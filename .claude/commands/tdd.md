---
description: Start a TDD cycle for one behaviour, stopping after the red test for review
argument-hint: <behaviour, e.g. "back-to-back bookings do not conflict">
---

Start a test-driven cycle for this behaviour: $ARGUMENTS

1. Find the matching row in `docs/edge-cases.md` (add one if missing and tell me).
2. Decide the right suite (unit vs integration) per `.claude/rules/testing.md` and explain why in one line.
3. Write the smallest failing test that expresses the behaviour. Add production types only as far as
   needed to compile (empty members or `throw new NotImplementedException()`).
4. Run the test and show that it fails **for the expected reason**.
5. **Stop.** Summarise the test and wait for my approval before implementing.

After approval: implement the minimum to pass, run the relevant suite, refactor with tests green, run
`/verify`, and update the "Test" column in `docs/edge-cases.md`.
