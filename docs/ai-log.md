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
