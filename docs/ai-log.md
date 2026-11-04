# AI collaboration log

Running record of how AI tools were directed, challenged and verified. Source material for the
README's **AI Collaboration Narrative** and the video. Add entries with `/ai-log`.

Tools: Claude Code (primary agent).

---

### 2026-10-29: Scenario analysis
- **Phase:** design
- **I asked the AI to:** break down all four scenarios: business need, hardest problem, edge cases.
- **The AI produced:** a comparison recommending Scenario A (most business logic, a real concurrency
  problem) or D (most balanced for full-stack, closest to Keyloop's integration business).
- **What I accepted, changed or rejected, and why:** I cross-checked against a second AI's comparison,
  which favoured D and claimed A's race condition is hard to demo. I challenged both: the race can be
  demonstrated with N parallel requests (1 success, N−1 conflicts), and D's backend core is thinner than
  it looks because two of its three requirements are UI requirements.
- **How I verified it:** re-read the brief's evaluation criteria ("tests that validate the core business
  logic"; "process for verifying the AI's work") and scored A and D against them.
- **Lesson:** use AI for breadth, then decide against the actual grading criteria.

### 2026-10-30: Chose Scenario A; repository and AI guardrails set up first
- **Phase:** design
- **I asked the AI to:** set up the project structure before any code, including Claude Code configuration.
- **The AI produced:** a Clean Architecture solution (.NET 10), `CLAUDE.md`, path-scoped engineering rules,
  `/verify`, `/tdd`, `/ai-log` commands, `code-reviewer` and `test-auditor` agents, and two hooks.
- **What I accepted, changed or rejected, and why:** I rejected the first `CLAUDE.md` draft as too thin
  and required principal-level standards covering Clean Architecture, DDD, TDD, SOLID/DRY, N+1, I/O,
  connections, race conditions, async/await, threading, logging and exception handling.
- **How I verified it:** the solution builds with warnings-as-errors; both hooks were pipe-tested
  (the guard blocks force-push / `reset --hard` / recursive deletes and allows normal commands; the
  formatter fixed a deliberately mis-indented file).
- **Lesson:** invest in guardrails first, so every later AI change is checked by the same rules.

### 2026-10-30: Edge-case audit of the AI-drafted system design
- **Phase:** design
- **I asked the AI to:** list its open questions about `system-design.md` and check whether every edge case was covered.
- **The AI produced:** an audit of its own v0.1 draft that found 6 defects:
  1. EC-11 (DST "nonexistent local time") could never happen, because requests carry an offset.
  2. Technician allocation (least loaded) contradicted bay allocation (best fit) and could waste specialists.
  3. A lost commit acknowledgement would turn a successful booking into a false `409`.
  4. The vehicle check could not see bookings at other dealerships.
  5. Nothing stopped a technician or bay from another dealership being assigned.
  6. The `Completed` status was unreachable.

  It also proposed about 30 missing edge cases and asked 4 business questions.
- **What I accepted, changed or rejected, and why:**
  - I decided the 4 questions: best-fit technician allocation; the buffer may run past closing; alternatives searched up to 7 days ahead; `422 ServiceNotOfferedAtDealership` for services a dealership can never do.
  - I accepted the remaining defaults: 90-day horizon, cancellation rules, no holidays or split shifts.
  - I marked ADR-0002 *Accepted* **provisionally**: good enough to build and test against, to be revisited once tests and the demo exercise them.
  - One proposed case (grid alignment in +05:30 zones) was dropped, because all real UTC offsets are multiples of 15 minutes, so it could never fail.
- **How I verified it:** _TBD: my own review of the updated diagrams and the edge-case catalogue._ All 10 Mermaid diagrams re-validated with the Mermaid parser.
- **Lesson:** ask the AI to attack its own design before writing code. Design defects are cheapest to fix on paper, and a test case that can never fail is as useless as a missing one.

### 2026-10-30: Independent AI review of step 2 (validation), then mutation testing
- **Phase:** implementation (TDD), step 2: booking request validation
- **I asked the AI to:**
  - make a `code-reviewer` run mandatory before every commit (added to `CLAUDE.md` and `/verify`);
  - fix the review findings and "make sure the tests are meaningful".
- **The AI produced:** a separate reviewer agent, with no memory of writing the code, found no correctness bugs but showed the tests were weaker than they looked:
  - **Tests that couldn't catch two bugs:** swapping two validation rules left all 33 tests green, and reading the weekday from UTC instead of local time also passed, because London is too close to UTC.
  - **A test helper that hid bad inputs:** it silently shifted times that don't exist on clock-change days.
  - **Objects that didn't enforce their own rules:** an inverted opening range (18:00–08:00) could be created, and the schedule kept a shared reference to the caller's dictionary.
  - **A wrong DST example in the rules document.**
- **What I accepted, changed or rejected, and why:**
  - **Fixed now:** the cheap, meaningful items: tests for rule order, an Auckland (UTC+13) test pinning the local weekday, invariants in `LocalTimeRange`, an immutable `WeeklySchedule`, and a nullability-safe `Result<T>`.
  - **Deferred explicitly:** full DST and Step 3/4 items, recorded in `docs/edge-cases.md` → *Deferred*.
  - **I corrected the AI's test design:** it put dealership settings into the booking builder; I asked for the builder pattern, so it extracted a separate `DealershipBuilder`.
- **How I verified it:**
  - **Mutation test (first run):** a script injected 16 deliberate bugs, one at a time.
  - **First claim, and its correction:** the AI first reported "16/16 caught". On re-review, the reviewer agent proved this wrong: one mutation never compiled, because of a PowerShell operator-precedence bug in the script (true result: 15 killed, 1 invalid). The AI had wrongly blamed the failure on a file lock, and had confirmed that case only with a separate manual run. The script was fixed and re-run (next entry).
  - **Build:** 0 warnings; 45/45 tests pass.
- **Lesson:** passing tests prove little on their own. Asking "would this test fail if the code were wrong?", and then actually breaking the code to check, found gaps that line coverage never would. Verification evidence needs verifying too: the same scrutiny caught a false "16/16" in the AI's own report.

### 2026-10-30: Business-domain review of the step 2 tests (`domain-analyst` agent)
- **Phase:** implementation (TDD), step 2
- **I asked the AI to:** create an agent that understands the business domain, because mutation testing only shows that the *existing* tests catch bugs, not whether the *right situations* are tested.
- **The AI produced:** a `domain-analyst` agent (role: service advisor and workshop manager). Its first run found that every *accepted* booking in the tests was "tomorrow, on the hour, on a weekday". So rules that are too strict would pass all tests while losing real business:
  - rejecting same-day walk-ins;
  - allowing hourly starts only (3 of every 4 slots lost);
  - treating Saturday as closed;
  - checking dates in UTC (Auckland would lose midday slots).

  It also raised 3 business questions: minimum notice, one error vs two, and fleet vehicles.
  The re-run of `code-reviewer` added 3 more bugs the tests missed: "past" compared on the typed clock, the grid checked on the request's offset, and the midnight check using UTC dates.
- **What I accepted, changed or rejected, and why:** I decided the 3 questions (ADR-0002 #14–16): no minimum notice, one `OutsideOpeningHours` error, fleet/authorised drivers out of scope. I accepted the new scenarios EC-58 to EC-61, business-language test names, and opening hours stated inside each test.
- **How I verified it:**
  - **New tests:** one theory of varied accepted bookings, plus the reviewer's 3 probe tests; 59/59 pass.
  - **Mutation re-run:** the fixed script ran on a **copy** of the repo, with 22 mutations including the 6 business-level bugs the agents described. All 22 compiled and all 22 were caught by a failing test.
  - **Final review:** the reviewer re-ran the mutations on its own copy and confirmed 22/22. It found 2 tests that would break once the 90-day horizon rule arrives (now fixed by using an in-horizon "now"). Under its new "cite the rule" instruction it also listed 6 rule gaps where the rules contradicted the code:
    - Fixed in the rules: grid size, how `now` reaches the domain, use of `DateTime`.
    - Fixed in the repo: the mutation script now lives at `tools/mutation/mutate.ps1` and always works on a temporary copy.
    - Deferred: the `DealershipTimeZone` value object.
- **Lesson:** mutation testing only finds the bugs you think of. A domain expert finds the situations nobody thought of, and accepted cases need as much variety as rejected ones. Requiring reviewers to cite a rule also exposes where the rules themselves are wrong.

### 2026-10-31: Business rule review; cut the plan to the core
- **Phase:** design
- **I asked the AI to:** walk me through every business rule in plain Vietnamese, separating what comes from the brief from what it had assumed.
- **The AI produced:** a review of validation rules 1–9 and ADR-0002 #1–21, recommending I keep all of them.
- **What I accepted, changed or rejected, and why:** I told the AI not to push scope too far. The plan is now allocation, database and `POST`, constraints and the concurrency test, then Docker, README and the video; everything else is parked (ADR-0002 *Delivery order*). I briefly changed #1 so that customers book for themselves, and the AI updated the ADR, rules and design document to match. I then reversed it: the service advisor stays the main user, and those edits were undone. After consulting another source I added a *Users and personas* section (vehicle owner as a future channel; resource management out of scope) and wrote three user stories. Story US2 made the AI move alternatives in the `409` out of the parked list.
- **How I verified it:** the AI reviewed its own diff before undoing the edits, so only the customer wording was reverted. The step 3 tests were unaffected (fingerprint unchanged).
- **Lesson:** a change of business actor reaches many documents, so settle it before editing them.

### 2026-10-31: Step 3 allocation: review found a gap that 37 of 37 mutations missed
- **Phase:** implementation (TDD), step 3
- **I asked the AI to:** implement allocation against the frozen red tests, then verify and review it through the orchestrated flow.
- **The AI produced:** `SlotAllocator`, the best-fit policies and rule 3. The implementer reported all tests passing and all mutations killed; the orchestrator re-ran both itself and got the same result. `code-reviewer` found no booking-rule bug in the code but raised 3 major findings:
  - No test ever accepted a specialised job. Every EV test expected a rejection, so a bug refusing all EV and Alignment jobs would pass the whole suite.
  - The design document listed alternatives as parked while the ADR scheduled them.
  - The orchestrator's own rewording of the approval gates left the automatic path with no step that freezes the tests, and contradicted three other files.
- **What I accepted, changed or rejected, and why:** the orchestrator fixed all three. The new tests (EC-71) first used a dealership with only specialists; `domain-analyst` answered "not ready" because a rule 3 bug at a normal mixed dealership would still pass, so the tests were changed before being frozen. The orchestrator also had a combined mutation split in two, because killing one half proved nothing about the other. Minor findings went to *Deferred* in `docs/edge-cases.md` rather than growing the scope.
- **How I verified it:** the orchestrator re-ran the gate: 114/114 unit tests, 41/41 mutations, tests fingerprint unchanged, format clean. Integration tests were not run: Docker is off and the project has no tests yet. A second `code-reviewer` pass reported no remaining blocker or major.
- **Lesson:** a perfect mutation score only grades the mutations someone thought of. All 37 passed while a whole class of accepted jobs was untested, so accepted cases must be reviewed from the business side too.

### 2026-10-31: Stopped at 114 tests and asked why they existed
- **Phase:** testing
- **I asked the AI to:** explain why there were so many tests, because I could not say why they existed. What I need to know is whether a dealer can create a schedule: which vehicle, which bay, whether it clashes.
- **The AI produced:** a breakdown by business question. It admitted no test covered that flow, because no `Appointment`, database or `POST` existed yet. The tests had grown from the rule list and the review and mutation loop, without me seeing a scenario list first.
- **What I accepted, changed or rejected, and why:** I removed three groups: time zones and summer time, which error wins when several rules fail, and technical foundation tests. That took the suite from 114 to 77 test cases. I also changed the direction: build the full flow end to end first, with no branching; branches come later. The AI recommended keeping those tests, and I decided otherwise. The cost is known: 11 mutations were removed because nothing catches them any more, including a dealership accepting another dealership's resource. These are listed in `docs/edge-cases.md`, *Deferred*.
- **How I verified it:** the orchestrator re-ran the build, unit tests, format check and mutation script after the removal.
- **Lesson:** a large, green test suite is not evidence that the product works. Tests should start from the scenarios I can explain, and the AI must show them to me before writing them.

### 2026-10-31: Removed time zones and UTC after checking the brief myself
- **Phase:** design, implementation (step 4)
- **I asked the AI to:** start the red phase of step 4, one test for the straight booking path (EC-72). When I saw London in the test, I asked why London, and then why we need time zones at all.
- **The AI produced:** it had chosen `Europe/London` itself, and it told me the brief requires dealerships to have an IANA time zone. That was false: the brief only says "at a desired time" and never mentions time zones. The assumption came from an earlier AI session and had spread through the rules, the design document, the domain and the tests. The AI's first reaction to my questions was to keep working on it: it moved the test to Ho Chi Minh and then recommended keeping the existing time-zone code.
- **What I accepted, changed or rejected, and why:** I opened the brief, rejected the AI's edit and decided on one clock: no time zones and no UTC (ADR-0002 #25). `10:00` now means 10:00 in the request, the response and the database. The AI then showed me a plan, which I approved. Domain and contract use plain `DateTime`, columns are `timestamp`, constraints use `tsrange`, and a request with an offset must get a `400` (EC-30). The cost is known: every dealership is treated as being on the same clock, and "now" relies on the server clock. Unit tests went from 77 to 73 (two tests and two theory cases only about zones or offsets) and five mutations were removed, with one added back for workload per date. I also told the AI to separate what the brief says from what the project assumed; opening hours are an assumption too, and I kept them in their simplest form.
- **Also in this step, a defect found in review:** `code-reviewer` found that the `CHECK` constraints accepted `24:00`, although the design note in `docs/system-design.md` §6.2 claimed they made it impossible. It reproduced the failure against real PostgreSQL: one such row in opening hours or a technician shift made bookings at that dealership return `500`. The migration had not been committed, so it was fixed in place with `closes_at < '24:00'` and `ends_at < '24:00'`, and the note was corrected. The review also confirmed that EC-30 is a live gap: `12:00+03:00` books 16:00 with a `201` on this machine. That branch is now the first follow-up.
- **How I verified it:** the orchestrator re-ran the gate itself: build clean, `PostAppointment_WhenTechnicianAndBayAreFree_ReturnsCreatedAndStoresConfirmedAppointment` passing, 73/73 unit tests, 26/26 mutations, format clean, tests fingerprint unchanged after the green phase. The `24:00` fix was checked in a throwaway `postgres:16.15` container (`23514` on both tables; `23:45` accepted) and again by `code-reviewer`, which then reported "clean". EC-57 still has no automated test.
- **Lesson:** an AI can state its own assumption as if it were a requirement, and then defend the work built on it. When a concept seems unnecessary, I should check the source document myself. The same goes for design notes: the `24:00` protection was written down as fact and was never true until someone ran it.

### 2026-11-02: The orchestrator overstated test coverage, and the reviewer caught it
- **Phase:** testing, review (step 4, requirement 2 through the API)
- **I asked the AI to:** re-read the brief with me, focus on one thing, and prove requirement 2 end to end: a second vehicle asking for a technician and bay that are already busy (EC-73 same time, EC-02 partial overlap, EC-01 straight after; `docs/work-packages/step-4-capacity.md`).
- **The AI produced:** three integration tests in `PostAppointmentTests`, a per-test `DELETE FROM appointments` reset and a second vehicle added by test setup. They passed at once, because `SlotAllocator` already had the rule, so the orchestrator applied four mutations by hand to a copy of the repository and all four were killed. But while packaging, the orchestrator removed the "whole-date busy window is untested" gap from `docs/edge-cases.md`, *Deferred*, and described mutation 4 as covering it. It also parked EC-30 as "not in the brief".
- **What I accepted, changed or rejected, and why:** I accepted the tests. `code-reviewer` ran the real mutation (the handler reads only the requested slot instead of the whole date) and all 77 tests still passed, so a technician's other jobs that day would silently count as zero workload (ADR-0002 #20). The gap was restored, mutation 4 was reworded, and `.claude/rules/testing.md` now says that hand-applied integration mutations and their results are recorded in the work package. The same review showed that "not in the brief" hid a breach of invariant 3: an offset on `requestedStart` books another time with `201`. The record now says so, and the decision was mine: clients always send a plain time, and an offset is out of scope and not checked (ADR-0002 #25, commit `9c768a6`).
- **How I verified it:** the hand-applied mutation results are in `step-4-capacity.md`, *Results*. Removing the bay busy check survives the integration tests, because the technician check comes first with one technician and one bay, but 9 unit tests kill it. `domain-analyst` said "ready". `/verify` gave build clean, 73/73 unit, 4/4 integration and format clean. Tests fingerprint: `5d89073538e92e49c957f54b61efb04bf9abb7d4`.
- **Lesson:** a test that passes at once proves nothing until a mutation fails against it, and the mutation has to be the bug that is claimed. Deciding by "the brief does not ask for it" must not hide that a stated invariant is broken.

### 2026-11-03: A wrong fix for a real race, measured and replaced
- **Phase:** implementation, review (step 5, the race loser gets `409`, not `500`)
- **I asked the AI to:** make the loser of a race get `409` instead of `500` (EC-17: 20 parallel requests for the last slot; EC-74/75: a request that read stale busy intervals). When `code-reviewer` found that the losers could also deadlock, I told it to fix that now, because it was "small".
- **The AI produced:**
  - The translation of the exclusion violation (`23P01`) to the capacity error worked.
  - `code-reviewer` then reproduced a PostgreSQL deadlock (`40P01`): 38 of 760 losers in its run still got `500`.
  - The orchestrator packaged a fix, "save again after a deadlock", on the premise that the winner has committed by the next attempt. The premise was wrong. Looped 30 times, the fix failed 3 runs, each with 19 `500`s and about a minute long. Without it, 1 of 30 failed.
- **What I accepted, changed or rejected, and why:**
  - The AI said its premise was wrong and stopped instead of trying a third idea. I approved ordered row locks: one short transaction locks the technician row, then the bay row (`FOR NO KEY UPDATE`), before the insert. The exclusion constraint stays the final guard (ADR-0001).
  - `code-reviewer` found that deleting either lock still passed every test, because EC-17 has one technician and one bay. I chose to add tests: EC-77 (different technicians, same bay) and EC-78 (same technician, different bays), calling the repository directly.
  - Their first version caught a missing lock 0–1 times in 10. A version that holds the rows in the database and releases all 20 saves together caught it about half the time.
  - The reviewer then showed that each lock is in fact caught on every run, by the other test: without the bay lock, a save does not wait on the held row and finishes early. I asked the AI to explain it again in plain words before accepting it.
- **How I verified it:**
  - Race tests: 30/30 runs pass with the locks (implementer), and 15/15 re-run by the orchestrator.
  - No locks: 7 of 30 runs fail with `500`.
  - Bay lock removed: killed 10/10. Technician lock removed: killed 10/10.
  - `code-reviewer` also confirmed the lock mode in a throwaway PostgreSQL: 0 deadlocks in 114 losers with both locks, 38 in 76 with the technician lock only.
  - Results are in `docs/work-packages/step-5-race-to-409.md`.
- **Lesson:** a fix for a race must be measured over many runs, not reasoned about. And the tests for a concurrency guard must be checked by deleting the guard, because a single shared technician and bay hid that each lock was untested.

### 2026-11-03: Minimal logging; the orchestrator recorded a red result it had not read
- **Phase:** implementation, review (step 4 logging, EC-79 to EC-82)
- **I asked the AI to:** add the minimum logging the rules ask for, after Docker and the README:
  - one `AppointmentBooked` entry per booking;
  - one `BookingRejected` entry at `Information` per rejection;
  - a generic `500` that is logged once and leaks nothing;
  - a `400` for a malformed body.
- **The AI produced:**
  - Four integration tests with a hand-written capturing logger.
  - Two `[LoggerMessage]` events in the Api layer.
  - One `IExceptionHandler`, plus `ThrowOnBadRequest = false` and `UseStatusCodePages()`, so a bad body stays a `400` problem in every environment.
  - The orchestrator recorded EC-82 as "already covered" (passing before any code was written).
- **What I accepted, changed or rejected, and why:**
  - I accepted the design.
  - `code-reviewer` ran EC-82 against the code from before green and showed it had failed for the right reason: the Development exception page logged a client's malformed body at `Error`. The orchestrator had truncated the test output and repeated the implementer's prediction. The record was corrected, and `.claude/rules/testing.md` now requires the red record to quote each test's actual result.
  - The reviewer also showed that mutation 7b (no custom handler in Development) survives only because the framework fallback behaves almost the same, and that a bad body returned an empty `400`. That was fixed with `UseStatusCodePages()`.
- **How I verified it:**
  - Orchestrator re-runs: build clean, 73/73 unit, 13/13 integration, format clean, `mutate.ps1` 26/26, test fingerprint `332fc9c5` unchanged.
  - Hand mutations: 1–6, 7a and 8 killed; 7b survives outside the specified contract.
  - The implementer probed the real API: `400`, `422`, `201` and `409` all came back as problem+json with a `traceId`.
- **Lesson:** an "already covered" claim is a result, not a prediction. It has to be read from the full test output, because a truncated log hid the one line that mattered.

### 2026-11-03: I rejected tests that were only correct because of the seed data
- **Phase:** testing (step 8)
- **I asked the AI to:** add more vehicles, technicians, shifts and skills to the seed, so the demo could show more than one technician and one bay.
- **The AI produced:**
  - It warned that ten test classes would break if the seed changed. For example, the race test "20 requests, exactly one wins" was only true because the seeded dealership had one technician and one bay.
  - It proposed adding a separate demo dealership, so the old tests would not have to change.
- **What I accepted, changed or rejected, and why:**
  - I rejected that. A test must prove that the code matches the business, not be defined to fit the data.
  - I also rejected the next proposal, which reworked only the old tests' Arrange, and asked for the suite to be rewritten from the business scenarios.
  - `domain-analyst` produced 26 scenarios for the brief's three requirements. I approved them, with three decisions recorded as ADR-0002 #26–28:
    - the same vehicle twice becomes `409 VehicleAlreadyBooked`;
    - race tests assert "never more than capacity" until the retry exists;
    - a test may store a cancelled appointment directly.
  - Every test now creates its own dealership with a test data builder, and no test references the seed.
  - Writing the scenarios exposed a real defect: booking the same car twice returned `500` whenever another technician and bay were free. It is now `409`.
  - The analyst also exposed a business gap that stays open: without the retry, simultaneous requests can be refused while a technician is still free.
- **How I verified it:**
  - Red: 2 failures for the right reason. Scenario 20 got `500`; the new error row threw `NotImplementedException`. Everything else passed at once.
  - `domain-analyst`: ready.
  - Green: 82/82 unit, 54/54 integration, `mutate.ps1` 27/27, race classes 15/15.
  - Hand mutations: every earlier one was re-run against the new suite, and all were killed except the recorded logging 7b. "The reader ignores bookings" is now caught by five tests instead of one.
  - `code-reviewer` found no blocker. Its two majors (the README out of step with the code) were fixed before commit.
- **Lesson:** shared seed data makes a test true by accident. A test should create the situation it names, so that reading it tells you the business case and changing the data cannot make it lie.
