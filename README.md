# Unified Service Scheduler

Appointment scheduling service for dealership service departments. A booking is confirmed only when a
compatible service bay **and** a qualified technician are both free for the whole service duration, and
the guarantee holds under concurrent requests.

> Keyloop technical assessment, Scenario A (Ownership domain). Backend is the implemented layer.
> **Status:** work in progress.

## Quick start

_To be completed: `docker compose up` for API + PostgreSQL + Aspire dashboard, and seeded demo data._

## Running the tests

```bash
dotnet test tests/ServiceScheduler.UnitTests   # no dependencies
dotnet test ServiceScheduler.slnx              # full suite; requires Docker for PostgreSQL (Testcontainers)
```

## API

_To be completed: endpoint summary, OpenAPI link, and `.http` examples._

## Design

- [System design document](docs/system-design.md)
- [Project structure guide](docs/project-structure.md)
- [Architecture decision records](docs/adr/)
- [Edge-case catalogue](docs/edge-cases.md)

## Project structure

```
src/
  ServiceScheduler.Domain          Booking rules, aggregates, value objects (no dependencies)
  ServiceScheduler.Application     Use cases and ports
  ServiceScheduler.Infrastructure  EF Core + PostgreSQL
  ServiceScheduler.Api             HTTP endpoints and composition root
tests/
  ServiceScheduler.UnitTests
  ServiceScheduler.IntegrationTests
docs/                              Design, ADRs, edge cases, AI log
.claude/                           AI agent configuration: rules, commands, agents, hooks
```

## AI Collaboration Narrative

_To be completed from [`docs/ai-log.md`](docs/ai-log.md). Planned structure:_

1. **Strategy:** guardrails before code: `CLAUDE.md`, path-scoped rules, hooks, review agents.
2. **Directing the AI:** plan-first, test-first (`/tdd`), small reviewed increments.
3. **Verifying and refining:** where AI output was wrong and how it was caught (tests, review agent, manual review).
4. **Ensuring final quality:** quality gate (`/verify`), concurrency test, independent review.
