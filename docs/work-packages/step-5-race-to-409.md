# Work package: step 5, the loser of a race gets `409`, not `500`

- **Branch:** `feature/allocation` (same pull request)
- **Status:** scenarios approved by the developer on 2026-11-02; `domain-analyst` found no open questions
- **Rules:** `.claude/rules/booking-domain.md` (*Allocation policy* 3, *Validation* 9), `persistence.md` (*Schema and constraints*),
  `errors-logging-observability.md`, `testing.md`; ADR-0001; ADR-0002 #17

## Scope

Two advisors read the busy intervals before either saves, both choose the same technician or bay, and the second save is rejected by
PostgreSQL's exclusion constraint (`23P01`). Today that is a `500`. The loser must get a `409` naming the resource that conflicted.

**Out of scope, do not build:** retry with another candidate (the next package, EC-18), the vehicle constraint and rule 8 (a vehicle
`23P01` stays an unhandled exception), alternatives, logging, idempotency, EC-45.

## Scenarios

Seed: one `General` technician, one `Standard` bay, oil change 60 minutes. Each request uses a different vehicle, added by the test setup.

| EC | Scenario | Expected (source) |
|---|---|---|
| EC-17 | 20 requests for Tuesday 10:00, sent in parallel through HTTP and released together by a gate | Exactly one `201`; the other 19 are `409` with `code` `NoQualifiedTechnicianAvailable`; no `500`; exactly one row in `appointments` (`testing.md`, *Must-have coverage*; ADR-0002 #17) |
| EC-74 | The first booking at 10:00 is saved. A second request, for another vehicle, reads the busy intervals **as they were before** the first was saved, so it chooses the same technician and bay, and its save is rejected on the technician constraint | `409`, `application/problem+json`, `code` `NoQualifiedTechnicianAvailable`; still exactly one row (*Allocation policy* 3: no retry yet, so the error names the resource that conflicted; ADR-0001) |
| EC-75 | A second `General` technician is added by the test setup. An appointment at 10:00 holding that **second** technician and the only bay is inserted directly in the database. A request at 10:00 reads the busy intervals as empty, chooses the first technician and the only bay, and its save is rejected on the bay constraint | `409`, `code` `NoCompatibleBayAvailable`; still exactly one row (the constraint name tells which resource conflicted) |

**Why EC-74 and EC-75 use a stale reader:** in the parallel test, a loser that reads after the winner has saved is turned away by the
normal check and never reaches the constraint, so EC-17 alone can pass with today's `500` path. A test `ISchedulingReader` that wraps the
real one and returns no busy intervals makes the race happen every time. The save and the constraint stay real.

## Test setup

- Extra vehicles and the second technician (with a Tuesday 08:00–17:00 shift and the `General` skill) are added by test setup over Npgsql,
  idempotently with fixed IDs. Not in the migration seed. The per-test reset deletes `appointments`; extra technicians must not change
  what EC-72 and the capacity tests allocate, so add the second technician **only** in EC-75's own setup and remove it at the end of that
  test, or use a separate fixture for it. State which you chose.
- The stale reader is swapped in with `fixture.WithWebHostBuilder(... ConfigureTestServices ...)`.
- EC-17 releases its requests together (a `TaskCompletionSource` or `Barrier` gate), never with `Task.Delay`.

## Code shape

- **Infrastructure:** `AppointmentRepository` catches `PostgresException` with `SqlState == PostgresErrorCodes.ExclusionViolation` and maps
  `ConstraintName` `ex_appointments_technician_overlap` → `BookingErrors.NoQualifiedTechnicianAvailable` and `ex_appointments_bay_overlap`
  → `BookingErrors.NoCompatibleBayAvailable`. Anything else is rethrown with `throw;`.
- **Application:** `IAppointmentRepository.AddAsync` returns a result that can carry that error; the handler returns it. No new error code.
- **Api:** nothing new; the existing mapper already turns `Conflict` into `409`.
- No raw provider exception crosses into Application.

## Approved extras

No NuGet package and no change to the public API contract (`409` with these codes is already documented). If either seems necessary, stop and report.

## Mutations

`tools/mutation/mutate.ps1` runs the unit tests only, so it gets none of these. The orchestrator applies each one by hand to a copy and runs
the integration tests; each must fail, and the results are recorded below (`testing.md`).

1. No translation: the `23P01` is not caught (`500`). EC-74 must fail.
2. Every `23P01` maps to the technician error. EC-75 must fail.
3. The error is swallowed and the request returns `201`. EC-17 or EC-74 must fail.
4. The technician and bay exclusion constraints are dropped (in the copy's migration only). EC-17 must fail.

## Fix after review: deadlock (`40P01`)

`code-reviewer` showed that two conflicting inserts can wait on each other during the exclusion check, and PostgreSQL then aborts
one with `40P01 deadlock detected` instead of `23P01` (38 of 760 losers in its reproduction), so that loser still gets a `500`.
**First fix, rejected.** Save again on `40P01`, a bounded number of times. The premise ("the other transaction has committed by then")
was wrong: all the losers are still waiting on each other, so the retried insert joins the same knot. EC-17 looped 30 times: 3 runs
failed with 19 × `500` and took about a minute each; without the retry 1 of 30 failed. Removed.

**Fix (developer approved 2026-11-02): ordered row locks before the insert.** `AddAsync` opens one short transaction. It first locks
the technician's row and then the bay's row, always in that order: `SELECT 1 FROM technicians WHERE id = @id FOR NO KEY UPDATE`, then
the same for `service_bays`. Next it inserts and commits. Requests for the same technician or bay now queue instead of waiting on each
other inside the exclusion check, so there is no cycle. Each loser, when its turn comes, sees the winner's committed row and gets a
clean `23P01`, which is translated as above. `NO KEY UPDATE` rather than `UPDATE` so the foreign-key checks (`KEY SHARE`) from
other inserts are not blocked. The exclusion constraints stay the final guard (ADR-0001); the locks only serialise contenders. Same
technician and bay only: two requests for the same **vehicle** on different technicians and bays can still deadlock (rule 8, deferred).
EC-76 records it. EC-17 looped at least 30 times is the cover. Mutation 5, "no locks", is checked the same way and is expected to
bring the occasional `500` back; because it is not deterministic, its result is recorded as observed.

## Follow-up after the second review: each lock needs a test (developer approved 2026-11-02)

`code-reviewer` showed that deleting the bay lock passes every test. EC-17 has one technician, so the technician lock alone already
queues every contender. The orchestrator found the mirror image too: EC-17 has one bay, so the bay lock alone also queues them, and
deleting the technician lock passes as well. The code is correct (the reviewer's PostgreSQL experiment: 0 deadlocks in 114 losers with
both locks, 38 in 76 with the technician lock only). What is missing is a test that needs each lock.

Through the API, every request is allocated the same technician and bay, so these tests call `IAppointmentRepository.AddAsync`
directly: 20 saves in parallel, each in its own DI scope (its own `DbContext`), released together by a gate.

| EC | Scenario | Expected |
|---|---|---|
| EC-77 | 20 saves at Tuesday 10:00, each with a **different technician** and the **same bay**, and a different vehicle | Exactly one success; the other 19 are `NoCompatibleBayAvailable`; no exception; exactly one row |
| EC-78 | 20 saves at Tuesday 10:00, each with the **same technician** and a **different bay**, and a different vehicle | Exactly one success; the other 19 are `NoQualifiedTechnicianAvailable`; no exception; exactly one row |

Setup: a class of its own with its own fixture and database (as `BayRaceTests`), so the extra technicians and bays never change
what other classes allocate. The extra technicians, bays and vehicles are inserted idempotently with fixed IDs. The repository does
not check skills or shifts, but give the rows valid values anyway.

These pass against today's code ("already covered"). They qualify when the mutations below fail against them:

6. Bay lock removed. EC-77 must fail.
7. Technician lock removed. EC-78 must fail.

Each mutation is run 10 times on a copy and its kill rate is recorded.

## Results (orchestrator, 2026-11-02)

Red: EC-74 and EC-75 failed with `500` from the unhandled `23P01` (technician and bay constraint); EC-17 failed with 14–17 of the 19 losers
getting `500`. `domain-analyst`: ready. Fingerprint of the approved tests: `e4f8795dbb55ea2d11a06eaf44b86efc74388937`, unchanged after green.

Green, re-run by the orchestrator: build clean, 73/73 unit, 7/7 integration in three runs, format clean.

| Mutation (applied to a copy) | Integration tests |
|---|---|
| 1. No translation | Killed (3 failed) |
| 2. Every `23P01` maps to the technician error | Killed (1 failed: EC-75) |
| 3. Error swallowed, `201` returned | Killed (3 failed) |
| 4. Technician and bay constraints never fire (empty ranges in the copy's migration) | Killed (3 failed) |

After the ordered-lock fix (re-run by the orchestrator): build clean, 73/73 unit, 7/7 integration, format clean, `BookingRaceTests`
15/15 in a loop (the implementer's own loop: 30/30, 7–11 s per run). Fingerprint unchanged. Mutations 1–4 re-run against the new code:
all killed.

| Mutation (applied to a copy) | `BookingRaceTests` looped 30 times |
|---|---|
| 5. No locks before the insert | 7 of 30 runs failed with `500`, each 25–29 s. The SQL state is not in the test output, so `40P01` is inferred, not shown |

Each lock's own test (`LockOrderRaceTests`, EC-77 and EC-78). Through the API every request gets the same technician and bay, so these
call the repository directly. A first version with only a gate killed mutation 6 in 0 of 10 runs and mutation 7 in 1 of 10: the inserts
rarely arrived within the same microseconds. The final version uses a holder transaction that locks the rows each save takes first,
waits in `pg_stat_activity` until all 20 saves are blocked, then commits so they wake together. The real code passed 15 of 15 runs.

| Mutation (applied to a copy) | Test, 10 runs |
|---|---|
| 6. Bay lock removed | EC-77 killed in 5 of 10 (`40P01 deadlock detected`, 19 of 20 saves threw) |
| 7. Technician lock removed | EC-78 killed in 6 of 10 (same failure) |

The remaining variation is the last client round trip that sends the `INSERT` after the lock is granted, which the test cannot hold.

`code-reviewer` then showed that each lock is caught on **every** run, by the other test. A foreign-key check (`KEY SHARE`) does not wait
for `NO KEY UPDATE`. So without the bay lock, a save in EC-78 inserts while the holder still has the bays, and the "a save finished
while the rows were held" check fails. EC-77 does the same for the technician lock. After the review fixes (comments say what each test
proves, a `try/finally` so a failed run leaks no saves, client backends only, 10 s wait), whole-class runs on fresh copies:

| Mutation | Killed | How |
|---|---|---|
| 6. Bay lock removed | 10 of 10 | EC-78 early finish 10/10; EC-77 deadlock 9/10 |
| 7. Technician lock removed | 10 of 10 | EC-77 early finish 10/10; EC-78 deadlock 5/10 |

Re-run by the orchestrator: build clean, 73/73 unit, 9/9 integration, format clean, all race classes 15/15 in a loop, and both mutations
killed in 2 of 2 runs each. `domain-analyst`: ready.
