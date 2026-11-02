# Unified Service Scheduler

Appointment scheduling service for dealership service departments. A booking is confirmed only when a
compatible service bay **and** a qualified technician are both free for the whole service duration, and
the guarantee holds under concurrent requests.

> Keyloop technical assessment, Scenario A (Ownership domain). Backend is the implemented layer.
## Quick start

Requires Docker.

```bash
docker compose up --build      # PostgreSQL 16 + the API on http://localhost:8080
docker compose down -v         # stop and remove the demo database
```

On startup the API applies the database migration, which includes the demo seed:

- dealership *Riverside*, open on Tuesdays 08:00–17:00;
- technician *Alice* (General skill);
- one Standard bay;
- a 60-minute oil change;
- a sample customer and vehicle.

Then send the requests in [`src/ServiceScheduler.Api/ServiceScheduler.Api.http`](src/ServiceScheduler.Api/ServiceScheduler.Api.http)
(VS Code REST Client, Rider or Visual Studio), or use `curl`:

```bash
curl -i -X POST http://localhost:8080/api/appointments -H "Content-Type: application/json" -d '{
  "customerId": "c0570000-0000-0000-0000-000000000001",
  "vehicleId": "ca700000-0000-0000-0000-000000000001",
  "serviceTypeId": "5e7c0000-0000-0000-0000-000000000001",
  "dealershipId": "dea10000-0000-0000-0000-000000000001",
  "requestedStart": "2026-11-10T09:00:00"
}'
```

| Request | Result |
|---|---|
| The booking above | `201 Created`, with the technician and bay assigned |
| The same request again | `409 NoQualifiedTechnicianAvailable`: Alice is already booked |
| An unknown dealership | `404 NotFound` |

Times are plain local times on one clock, with no offset (ADR-0002 #25). "Now" is the API container's clock, which is
`TZ=UTC` by default. Set `TZ` before `docker compose up` to change it.

## Building and running the tests

Requires the .NET 10 SDK. The integration tests also require Docker.

```bash
dotnet build ServiceScheduler.slnx                       # warnings are errors
dotnet test  tests/ServiceScheduler.UnitTests            # domain and application rules, no dependencies
dotnet test  ServiceScheduler.slnx                       # full suite: real PostgreSQL through Testcontainers
pwsh tools/mutation/mutate.ps1                           # injects known bugs; every one must be caught
```

What the integration tests prove:

| Test class | What it proves |
|---|---|
| `PostAppointmentTests` | Requirement 2: a busy technician or bay is refused with `409`, and back-to-back bookings are accepted |
| `BookingRaceTests` | 20 parallel requests for the last slot: exactly one `201`, nineteen `409`, no `500`, one row stored |
| `BayRaceTests` | A race lost on the bay is reported as `NoCompatibleBayAvailable` |
| `LockOrderRaceTests` | Each lock taken before the insert is needed. Without it, concurrent saves deadlock and fail with `500` |

## API

| Method and route | Success | Expected failures |
|---|---|---|
| `POST /api/appointments` | `201 Created` + `Location` | `400` malformed body, `404` unknown reference, `409` no technician or bay free, `422` business validation (vehicle not the customer's, past, outside opening hours, off the 15-minute grid, beyond the horizon, service not offered) |

Errors are RFC 9457 `application/problem+json` with a stable `code`.

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
tools/mutation/                    Mutation check: known bugs that the tests must catch
docs/                              Design, ADRs, edge cases, AI log
.claude/                           AI agent configuration: rules, commands, agents, hooks
```

## AI Collaboration Narrative

I used Claude Code as the main tool. Every notable interaction is recorded, with dates, in
[`docs/ai-log.md`](docs/ai-log.md). This section summarises it.

### 1. Strategy: guardrails before code

Before any production code, I had the AI set up the rules it would be held to:

- [`CLAUDE.md`](CLAUDE.md) states the four invariants (no double-booking, whole-duration fit, one clock,
  cancelled appointments never block).
- Path-scoped rules in `.claude/rules/` cover architecture, the booking domain, persistence, concurrency, the
  API, errors and testing. They load automatically for the files being edited.
- `/verify` (build with warnings as errors, tests, format) and `/tdd` commands.
- Hooks block destructive git commands and format edited files.

I rejected the first `CLAUDE.md` draft as too thin and asked for principal-level standards.

### 2. Directing the AI: an orchestrated team with me as the business owner

The main session acts as an orchestrator. It delegates to four agents with narrow roles:

| Agent | Role |
|---|---|
| `domain-analyst` | Derives business scenarios; it cannot edit files |
| `implementer` | Writes the tests and code for one approved package |
| `code-reviewer` | Reviews each diff before commit |
| `test-auditor` | Audits the tests against the edge-case catalogue |

Each step follows the same order:

1. **Scenarios.** The scenarios are shown to me in plain language, and I decide the business questions.
   Decisions go to [ADR-0002](docs/adr/0002-scope-and-business-assumptions.md).
2. **Red.** The tests are written red, checked to fail for the right reason, then **frozen** with a git fingerprint.
3. **Green.** The code is written against the frozen tests. The orchestrator re-runs every check itself instead
   of trusting reported numbers.

Work packages live in [`docs/work-packages/`](docs/work-packages/).

### 3. Verifying and refining: where the AI was wrong, and how it was caught

- **An invented requirement.** The AI told me the brief required dealership time zones; it does not. I checked the
  brief myself, rejected the time-zone work and chose one clock (ADR-0002 #25).
- **A false test result.** The AI reported "16/16 mutations caught". The reviewer proved one mutation never
  compiled, because of a bug in the mutation script.
- **Green tests that would miss a bug.** A perfect mutation score (37/37) hid the fact that no test ever accepted
  an EV or Alignment job. A bug refusing all of them would have passed the whole suite.
- **Overstated coverage.** The orchestrator described a mutation as covering a gap that it did not. The reviewer
  ran the real mutation, and all 77 tests stayed green.
- **A wrong fix for a real race.** Under 20 parallel requests, PostgreSQL sometimes aborted a loser with a
  deadlock, which became a `500`. The AI's first fix ("save again") rested on a wrong premise. A 30-run loop
  showed it failed, so it was removed and replaced with ordered row locks. The reviewer then showed that each lock
  needed its own test (ADR-0001).
- **Too many tests.** I stopped at 114 tests that I could not explain, removed three groups, and required every
  test to start from a scenario I can state in business terms.

### 4. Ensuring final quality

- The **database is the final guard** against double-booking: PostgreSQL exclusion constraints, tested against
  real PostgreSQL with Testcontainers ([ADR-0001](docs/adr/0001-database-enforces-no-double-booking.md)).
- **Mutation testing:** `tools/mutation/mutate.ps1` injects known bugs into a copy of the repository, and every
  one must be caught. Integration-only mutations are applied by hand, and their results are recorded in the work
  package.
- **Race tests are looped** at least 15 times before approval; a single green run proves little.
- An independent **`code-reviewer`** runs on every diff before commit. Every blocker or major finding is fixed or
  explicitly deferred in [`docs/edge-cases.md`](docs/edge-cases.md), *Deferred*.
