---
paths:
  - "tests/**"
---

# Testing standards (TDD)

## Workflow

Red → Green → Refactor. Write the test that expresses the rule, run it and see it fail for the
right reason, implement the minimum to pass, then refactor with the suite green. A bug fix always
starts with a test that reproduces the bug.

- The right reason is an assertion on the specified outcome, or `NotImplementedException` from a member
  added for this behaviour. A new test that passes immediately is reported as already covered; it is
  never reshaped until it fails.
- Once red tests are approved (`CLAUDE.md`, flow step 3) they are frozen (Arrange, Act and Assert, and the builders they
  use). Changing one afterwards needs the developer's approval and a stated reason.
- Mutations are designed from the business boundaries (by the orchestrator from the `domain-analyst`
  scenarios, or by the developer on the solo `/tdd` path), not by whoever wrote the tests being graded.

## What goes where

| Suite | Scope | Dependencies |
|---|---|---|
| `UnitTests` | Value objects, aggregates, `SlotAllocator`, handlers with in-memory fakes for ports | None. Runs in seconds |
| `IntegrationTests` | Repositories, migrations, exclusion constraints, HTTP endpoints via `WebApplicationFactory`, idempotency, concurrency | PostgreSQL via Testcontainers (Docker) |

- **Never use the EF Core InMemory provider or SQLite to test persistence behaviour.** They do not
  support exclusion constraints, `tsrange` or PostgreSQL concurrency semantics, so passing tests
  there prove nothing.
- Mock only ports you own and only at boundaries. Do not mock `DbContext`. Prefer hand-written fakes
  over mocking frameworks for simple ports.

## Conventions

- Name: `MethodOrBehaviour_Scenario_ExpectedOutcome`, e.g.
  `Book_WhenBayIsBusyForPartOfDuration_ReturnsNoCompatibleBay`.
- Arrange / Act / Assert, separated by a blank line. One behaviour per test.
- Test data via builders with sensible defaults (`AppointmentRequestBuilder.Default().At("10:00")`);
  a test states only the values that matter to it.
- Time is controlled with a small fixed `TimeProvider` subclass in the test project (Application and
  integration tests) or a fixed `now` argument (Domain tests). No `DateTime.Now`, no `Thread.Sleep`, no `Task.Delay` for synchronisation.
- A test's `now` must keep its requests inside the booking horizon unless the horizon is what it tests.
- Accepted cases need as much variety as rejected ones (same day, every quarter-hour, each weekday's
  hours); a rule that is too strict loses business just as a lax one causes errors.
- Mutation check: `tools/mutation/mutate.ps1` injects known bugs into a copy of the repo; every one
  must be caught. Add a mutation when you add a rule.
- Use `[Theory]` for boundary tables (back-to-back, touching edges).
- Tests are independent and order-agnostic; each integration test resets data (Respawn, a fresh schema, or deleting the
  tables a test writes while keeping the migration seed). Reference rows a test adds are inserted idempotently with fixed IDs.
- Mutations that only integration tests can catch are applied by hand to a copy of the repository; record each one and its
  result in the work package.
- Assertions use xUnit `Assert` (or Shouldly if approved). Do not use FluentAssertions v8+ (commercial licence).

## Must-have coverage

Every row in `docs/edge-cases.md` maps to at least one test; keep the "Test" column there up to date.
The concurrency test is mandatory:

> N parallel requests (N ≥ 20) for the last free slot, released together with a `Barrier`/
> `TaskCompletionSource` gate → exactly **one** `201`, the rest `409`, and exactly one row in the database.

## Quality bar

- Tests must be deterministic. A flaky test is a bug; fix or quarantine it with an issue, never retry it into green.
- Coverage is a signal, not a goal: aim for complete branch coverage of Domain and the booking handler.
