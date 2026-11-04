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
docker compose up --build                                # API (http://localhost:8080) + PostgreSQL; migrations and seed applied on startup
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
3. **One clock.** Every time is a plain date and time on one clock, with no time zones and no UTC
   conversion (ADR-0002 #25). "Now" comes from an injected `TimeProvider`.
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

## Agent team and orchestration

The main session is the **orchestrator**: it plans the step, writes each work package, delegates to
the agents below, checks their output against the business rules, and owns the developer approvals.
Subagents never talk to each other; everything goes through the orchestrator.

| Agent | Role | Edits files? |
|---|---|---|
| `domain-analyst` | Business scenarios (Given/When/Then) and open questions for a step | No |
| `implementer` | Writes tests (red) and code (green) for one approved work package | Yes, within scope |
| `code-reviewer` | Reviews the diff against the rules before commit | No |
| `test-auditor` | Audits test coverage and strength against `docs/edge-cases.md` | No |

Flow for each step (⏸ = developer approval):

1. **Analyse:** `domain-analyst` → scenarios and open questions → ⏸ developer decides the questions;
   the orchestrator records the decisions (ADR, `booking-domain.md`) and adds the `EC-` rows.
2. **Package:** the orchestrator writes the work package: scope, scenarios with `EC-` ids and sources,
   the mutations the tests must catch (from the analyst's boundaries), and any extras such as a package
   or API change. ⏸ the developer sees the main scenarios in plain language before any test is written,
   and approves any NuGet package, API contract change or business decision.
3. **Red:** `implementer` phase `red` → orchestrator checks each test fails for the expected reason and
   `domain-analyst` checks the tests express the scenarios. The tests are approved automatically when the
   analyst says "ready" with no open questions and the orchestrator's checks pass; otherwise ⏸ escalate
   to the developer. A test that closes a gap in correct code passes at once ("already covered"); it
   qualifies when a packaged mutation fails against it. Once approved either way, the orchestrator stages `tests/` (`git add tests/`) and
   records the fingerprint `git diff --cached -- tests/ | git hash-object --stdin`, so the tests are frozen.
4. **Green:** `implementer` phase `green` → orchestrator verifies itself, never trusting reported numbers:
   the fingerprint is unchanged, `git diff -- tests/` and `git status --short tests/` show no change to
   approved tests or builders (new builder methods only), the `mutate.ps1` diff contains the packaged
   mutations, and `/verify` steps 1–4 plus `pwsh tools/mutation/mutate.ps1` pass when re-run.
5. **Review:** `code-reviewer` → for `blocker`/`major` findings, `implementer` phase `fix`, or ⏸ developer
   defers. A finding that needs a new test becomes a new `red` package; an approved test change is
   re-staged and re-fingerprinted. Repeat flow steps 4–5 until clean.
6. **Commit:** orchestrator updates docs and `/ai-log`, commits on the `feature/*` branch → ⏸ developer
   approves the push and reviews the pull request before merge.
7. **Audit:** `test-auditor` after project step 5 (database constraints and concurrency) and before submission.

Ownership: the developer decides business rules; the orchestrator owns rules, ADRs, design docs, the
catalogue rows, the AI log and git; the `implementer` owns `src/`, `tests/`, `tools/mutation/` mutations
and the catalogue's Test column. `/tdd` is the solo path when no orchestration is used.

If a project agent is missing from the session's agent list (defined after the session started),
start a new session in the repo folder. Until then a substitute agent type may stand in only with the
agent file's full body pasted as its prompt: read-only roles via the built-in `Plan` type (no file-edit
tools, though it has Bash), and the `implementer` via `general-purpose` **only with the developer's
explicit approval** for that package, because its tool limits then rely on instructions alone.

Branches: `feature/*` from `dev`, pull request into `dev`; `dev` merges into `main` for a release.

## Working agreement for AI agents

1. **Plan first** for anything beyond a trivial edit: state the approach, the files affected and the
   edge cases from `docs/edge-cases.md` it covers. Wait for approval where the flow above marks ⏸.
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
