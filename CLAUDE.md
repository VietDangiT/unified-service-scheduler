# Unified Service Scheduler

Backend service that books dealership service appointments. A booking succeeds only when a
**compatible service bay** and a **qualified technician** are both free for the **entire** service
duration, inside the dealership's opening hours. Keyloop technical assessment, Scenario A.
The backend is the implemented layer; the client is represented by an OpenAPI contract, a `.http`
file and, optionally, a thin React demo page.

- Design: [docs/system-design.md](docs/system-design.md)
- Decisions and assumptions: [docs/adr/](docs/adr/)
- Edge-case catalogue (drives the test suite): [docs/edge-cases.md](docs/edge-cases.md)

## Commands

```bash
dotnet build ServiceScheduler.slnx                       # warnings are errors
dotnet test  tests/ServiceScheduler.UnitTests            # fast, no Docker required
dotnet test  ServiceScheduler.slnx                       # full suite; integration tests need Docker
dotnet format ServiceScheduler.slnx --verify-no-changes  # style gate
docker compose up --build                                # API + PostgreSQL + Aspire dashboard
```

## Architecture (Clean Architecture, DDD)

```
src/ServiceScheduler.Domain          Aggregates, value objects, domain services, domain errors. Pure C#.
src/ServiceScheduler.Application     Use cases (commands/queries), ports (interfaces), orchestration.
src/ServiceScheduler.Infrastructure  EF Core + Npgsql, migrations, repository/port implementations.
src/ServiceScheduler.Api             Minimal API endpoints, contracts, ProblemDetails, composition root.
tests/ServiceScheduler.UnitTests        Domain and application behaviour, no I/O.
tests/ServiceScheduler.IntegrationTests Real PostgreSQL (Testcontainers), HTTP pipeline, concurrency.
```

Dependency rule: `Api → Application → Domain`, `Infrastructure → Application`. Domain depends on
nothing in the solution and on no framework (no EF Core, no ASP.NET Core, no logging).

## Invariants that must never be broken

1. **No double-booking.** A technician, a bay and a vehicle can each hold at most one confirmed
   appointment at any instant. The final guard is a PostgreSQL exclusion constraint; application
   checks exist to choose resources and to return precise errors, never as the only protection.
2. **Whole-duration fit.** Bay, technician shift and opening hours must cover `[start, end)` entirely.
3. **Time is UTC** in storage and comparisons; dealership-local time (IANA zone) is used only to
   evaluate opening hours and shifts. "Now" comes from an injected `TimeProvider`.
4. **Cancelled appointments never block capacity.**

## Engineering standards

Detailed, path-scoped rules live in `.claude/rules/` and load automatically for the files being edited:

| Rule file | Covers |
|---|---|
| `architecture.md` | Clean Architecture boundaries, DDD tactical patterns, where logic belongs |
| `booking-domain.md` | Business rules, ubiquitous language, allocation policy |
| `csharp-design.md` | SOLID, DRY, design patterns, immutability, naming, nullability |
| `async-concurrency.md` | async/await, cancellation, thread safety, DI lifetimes, race conditions |
| `persistence.md` | EF Core, N+1, connections, transactions, migrations, constraints |
| `api-conventions.md` | REST contract, status codes, ProblemDetails, validation, idempotency, security |
| `errors-logging-observability.md` | Result vs exceptions, global handler, structured logs, traces, metrics |
| `testing.md` | TDD workflow, test pyramid, naming, fixtures, concurrency tests |

## Working agreement for AI agents

1. **Plan first** for anything beyond a trivial edit: state the approach, the files affected and the
   edge cases from `docs/edge-cases.md` it covers. Wait for approval before writing code.
2. **TDD:** write the failing test, run it red, implement the minimum to pass, refactor.
   At the start of each step, run the `domain-analyst` agent to derive the business scenarios to test;
   the developer decides its open questions before tests are written.
3. **Verify before claiming done:** run `/verify`. Never report success with a failing build or test.
   **Before every commit**, also run the `code-reviewer` agent on the diff and resolve or explicitly
   defer every `blocker` and `major` finding. Report what it found, even when nothing was found.
4. **Ask before:** adding a NuGet package, changing the public API contract, editing an applied
   migration, or weakening any invariant above.
5. **Keep docs in sync:** a changed decision updates its ADR and `docs/system-design.md` in the same change.
6. **Record notable AI interactions** (rejected suggestion, defect found in generated code,
   design correction) with `/ai-log`. This is evidence for the README's AI Collaboration Narrative.
7. Prefer the simplest design that satisfies the invariants. No speculative abstractions.
