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
