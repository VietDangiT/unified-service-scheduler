# Work package: step 4, a busy technician and bay through the API

- **Branch:** `feature/allocation` (same pull request as the booking flow)
- **Status:** scenarios and the change to the frozen test's class approved by the developer on 2026-11-02
- **Source:** the brief, requirement 2: check both a ServiceBay and a qualified Technician "for the entire service duration"
- **Rules:** `.claude/rules/booking-domain.md` (*Capacity rules*, *Overlap semantics*), `testing.md`; ADR-0002 #17

## Scope

Prove requirement 2 end to end, against real PostgreSQL. `SlotAllocator` already has the rule and its unit tests, so these tests are
expected to pass at once. Report that as "already covered"; never reshape a test until it fails. They qualify through the mutations below.
Change `src/` only if a test fails, and then report why first.

**Out of scope, do not build:** rule 8 (vehicle overlap in code), two simultaneous requests, translating `23P01`, alternatives,
input validation, logging.

## Scenarios

The seed has one technician and one bay. Each test first books the seeded vehicle at 10:00 on Tuesday `2026-11-03` **through the API**
(an oil change takes 60 minutes, so 10:00–11:00), then a **second vehicle** sends the request below.

| EC | The second vehicle requests | Expected (source) |
|---|---|---|
| EC-73 | 10:00, the same time | `409`, `application/problem+json`, `code` = `NoQualifiedTechnicianAvailable`. `appointments` holds only the first appointment (ADR-0002 #17: both are busy, the technician error wins) |
| EC-02 | 10:30, overlapping for part of the duration | The same as EC-73 |
| EC-01 | 11:00, straight after | `201`. Two rows: the first unchanged, the second 11:00–12:00 with the same technician and bay (half-open intervals) |

**Why a second vehicle:** when rule 8 is built, the same vehicle at an overlapping time gets `VehicleAlreadyBooked`, which is a different rule.
These tests must keep meaning "the technician and bay are busy" after that.

## Test setup

- **New tests go in `PostAppointmentTests`.** The EC-72 test method does not change by a single character; only the class declaration gains
  `IAsyncLifetime`. The developer approved this on 2026-11-02.
- **Data reset:** a fixture method that runs `DELETE FROM appointments` directly over Npgsql and keeps the seed rows, called before each test
  (`step-4-booking-flow.md`, *Follow-ups*).
- **The second vehicle** belongs to the seeded customer and is added by the test setup over Npgsql, with a fixed ID, idempotently. Not in the
  migration seed: EC-72 relies on the seed staying as it is.
- Read the database directly, as EC-72 does. Assert through the wire format, without referencing the Api contract types.

## Approved extras

No NuGet package and no change to the public API contract. If either seems necessary, stop and report.

## Mutations

`tools/mutation/mutate.ps1` runs the unit tests only, so it gets none of these. The orchestrator applies each one by hand to a copy of the
repository and runs the integration tests; each must fail:

1. The reader returns no busy intervals, so existing appointments are ignored.
2. The allocator's overlap check looks at the start time only, so a job already running at 10:30 is missed.
3. The overlap check treats touching intervals as overlapping, so 11:00 is refused.
4. The reader sees only appointments that start at or after the requested start, so a job already running is missed.

Not covered here: the whole-date window that workload needs. Reading only the requested slot still finds every overlapping job, so these tests
pass; it stays in `docs/edge-cases.md`, *Deferred*.

## Results (orchestrator, 2026-11-02)

The three tests passed at once ("already covered"). Each mutation was applied by hand to a copy of the repository, then the integration tests
were run:

| Mutation | Integration tests |
|---|---|
| 1. Reader ignores existing appointments | Killed (2 failed) at the time. **No longer true after step 5** (`test-auditor`, 2026-11-03): the constraint rejection is now also a `409`, so this mutation survives; see `docs/edge-cases.md`, *Deferred* |
| 2. Overlap compares start only | Killed (1 failed) |
| 3. Touching intervals overlap | Killed (1 failed) |
| 4. Reader sees only jobs starting at or after the request | Killed (1 failed) |
| Extra: the bay busy check removed | Survived: with one technician and one bay, the technician check comes first. Killed by 9 unit tests |

`domain-analyst`: ready, no open questions. Fingerprint of the approved tests: `5d89073538e92e49c957f54b61efb04bf9abb7d4`.
