# Work package: step 4, logging and unexpected errors

- **Branch:** `feature/allocation` (same pull request as the booking flow)
- **Status:** scenarios approved by the developer on 2026-11-02, then parked; resumed on 2026-11-03 after Docker and the README,
  before the video. Renumbered EC-79 to EC-82 (the old ids went to the capacity and race tests). The per-test reset and
  `IAsyncLifetime` already exist in `PostAppointmentTests` (`step-4-capacity.md`); this package only adds clearing the captured logs
  to that reset. Known interaction: EF Core logs a rejected insert at `Error` (a lost race), but no scenario here involves a race
- **Rules:** `.claude/rules/errors-logging-observability.md`, `api-conventions.md` (*Errors*), `testing.md`
- **Design:** `docs/system-design.md` §11 (the orchestrator updates it at commit)

## Scope

1. A successful booking writes one `AppointmentBooked` log entry.
2. A rejected booking writes one `BookingRejected` log entry.
3. An unexpected exception becomes a generic `500` ProblemDetails and is logged once at `Error`.

**Out of scope, do not build:** JSON console output, OpenTelemetry, traces, metrics, health checks, the invalid-request
branch (EC-30), logging in Application, Domain or Infrastructure.

## Scenarios

All through the real HTTP pipeline, against real PostgreSQL. Dates use the seed: a Tuesday is open, `2026-11-04` (Wednesday) is closed.

| EC | Scenario | Expected (source) |
|---|---|---|
| EC-79 | An advisor books the seeded vehicle at 10:00 on a Tuesday | `201`. Exactly one entry named `AppointmentBooked`, at `Information`, whose structured values `AppointmentId`, `DealershipId`, `TechnicianId`, `ServiceBayId` equal the IDs in the response. No captured entry from any category contains `Sample Customer`, `customer@example.com` or `+00 000 000 000`, in its message or its values (`errors-logging-observability.md`, *Structured logging*) |
| EC-80 | An advisor books at 10:00 on the closed Wednesday | `422`. Exactly one entry named `BookingRejected`, at `Information`, with `Reason` = `OutsideOpeningHours` and `DealershipId` = the seeded dealership. No entry named `AppointmentBooked`, no entry at `Error`, no row in `appointments` (*Levels*: expected rejections are `Information`) |
| EC-81 | The reader port throws an exception whose message contains a marker string, while booking | `500`, `application/problem+json`, with `status` 500, a `title` and a `traceId`. The body contains neither the marker nor the exception's type name nor a stack trace. Exactly one entry at `Error` across all categories, carrying that same exception. No `AppointmentBooked` or `BookingRejected` entry (`api-conventions.md`, *Errors*; *Exceptions vs results*) |
| EC-82 | Technical guard: the body is not valid JSON | `400` and no entry at `Error`. The test host runs in `Development`, where minimal APIs throw `BadHttpRequestException` for a bad body, and a global handler would turn that into a `500`. The behaviour must be the same in every environment (`api-conventions.md`, status table). EC-30 and the other shape errors stay in the next branch |

Entries are read by their `EventId.Name` and level, and by the key-value pairs of their state. The `EventId` names and the
value names above are the log contract; `Reason` holds the error's `Code`.

## Test setup

- **Hand-written capturing logger** in `tests/ServiceScheduler.IntegrationTests/Fixtures/`: an `ILoggerProvider` that records category,
  level, `EventId`, formatted message, state key-value pairs and exception in a thread-safe collection. No new package. The fixture owns one
  instance, exposes it and registers it in `ConfigureTestServices`.
- **Data reset:** `DeleteAppointmentsAsync` and `IAsyncLifetime` already exist. Clearing the captured logs is a **new** fixture
  method, called by the new test class's `InitializeAsync`; existing approved tests and fixture methods do not change.
- **New tests go in a new class** (for example `Api/BookingLogTests.cs`) with its own fixture instance, so approved test files stay
  untouched.
- **EC-81:** a hand-written `ISchedulingReader` that throws, swapped in with `fixture.WithWebHostBuilder(... ConfigureTestServices ...)`.
- Expect the `Development` environment that `WebApplicationFactory` uses: EC-81 and EC-82 must hold there.

## Code shape

- **Api only.** Logging lives in the endpoint, because Application has no logging reference and no new package is approved. Do not reference
  a logging package from Domain or Application.
- `[LoggerMessage]` methods define the two business events, with their `EventId` names. No string interpolation.
- One `IExceptionHandler`, registered with `AddExceptionHandler`, plus `UseExceptionHandler()`. It logs once at `Error` and writes the
  ProblemDetails through `IProblemDetailsService`. It never returns the exception's message, type or stack trace.
- Check that the framework does not also log the handled exception at `Error`; EC-81 counts entries across all categories.

## Approved extras

No NuGet package and no change to the public API contract. The `500` is already the documented behaviour. If either seems necessary, stop and report.

## Red results (orchestrator, 2026-11-03)

EC-79 and EC-80 fail because there is no `AppointmentBooked` or `BookingRejected` entry. EC-81 fails because the `500` body leaks the
exception's message: the Development exception page. `domain-analyst`: ready.

**Correction (after `code-reviewer`):** this record first said EC-82 "passes already". That was wrong. The orchestrator truncated the
test output and took the implementer's prediction instead of reading the fourth result. EC-82 fails red for the right reason: the
status is `400`, but `DeveloperExceptionPageMiddleware` logs the client's malformed body at `Error`. The reviewer confirmed this by
running EC-82 against the pre-green source. Fingerprint of the approved tests: `332fc9c5495cec437f87bb24df5a60ed9016fe01`.

## Green results (orchestrator, 2026-11-03)

Re-run by the orchestrator:
- build clean;
- 73/73 unit and 13/13 integration;
- format clean;
- `mutate.ps1` 26/26;
- fingerprint unchanged, `git diff -- tests/` empty.

| Mutation (applied to a copy by the implementer) | Integration tests |
|---|---|
| 1. Rejection logged at `Error` | Killed by EC-80 |
| 2. Fixed text instead of the code | Killed by EC-80 |
| 3. `AppointmentBooked` before the result check | Killed by EC-79 and EC-80 |
| 4. Customer's full name in the log | Killed by EC-79 |
| 5. Exception message in `detail` | Killed by EC-81 |
| 6. Handler returns without logging | Killed by EC-81 |
| 7a. `UseExceptionHandler()` only outside Development | Killed by EC-81 |
| 7b. `AddExceptionHandler` only outside Development | Survives: the difference is outside the specified contract. `UseExceptionHandler()` still runs, and the framework's own fallback writes a generic `500` and logs once at `Error`. Only the title, the event id and the method/path values differ. Production is unaffected |
| 8. `ThrowOnBadRequest = false` removed | Killed by EC-82 |

## Mutations

`tools/mutation/mutate.ps1` runs the unit tests only, so it gets none of these. After green, the orchestrator applies each one by hand to a copy of
the repository and runs the integration tests; each must fail:

1. A rejected booking is logged at `Error` instead of `Information`.
2. The rejection logs a fixed text instead of the error's code.
3. `AppointmentBooked` is written before the result is checked, so a rejection also writes it.
4. A log entry includes the customer's full name, read from the seeded row (personal data in the log).
5. The `500` puts the exception's message in `detail`.
6. The exception handler returns without logging.
7. The handler is registered only outside `Development`.
8. Bad-request exceptions are no longer kept out of the global handler (EC-82).
