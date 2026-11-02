# Work package: step 4, logging and unexpected errors

- **Branch:** `feature/allocation` (same pull request as the booking flow)
- **Status:** parked by the developer on 2026-11-02: business logic first. Nothing is built. Comes back after Docker and the README,
  before the video, cut down to the minimum; its rows return to `docs/edge-cases.md` then. **The EC-73 to EC-76 ids below are placeholders:**
  EC-73 now belongs to the capacity test, so they get new numbers. The per-test reset and `IAsyncLifetime` now exist
  (`step-4-capacity.md`); this package only adds clearing the captured logs to that reset
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
| EC-73 | An advisor books the seeded vehicle at 10:00 on a Tuesday | `201`. Exactly one entry named `AppointmentBooked`, at `Information`, whose structured values `AppointmentId`, `DealershipId`, `TechnicianId`, `ServiceBayId` equal the IDs in the response. No captured entry from any category contains `Sample Customer`, `customer@example.com` or `+00 000 000 000`, in its message or its values (`errors-logging-observability.md`, *Structured logging*) |
| EC-74 | An advisor books at 10:00 on the closed Wednesday | `422`. Exactly one entry named `BookingRejected`, at `Information`, with `Reason` = `OutsideOpeningHours` and `DealershipId` = the seeded dealership. No entry named `AppointmentBooked`, no entry at `Error`, no row in `appointments` (*Levels*: expected rejections are `Information`) |
| EC-75 | The reader port throws an exception whose message contains a marker string, while booking | `500`, `application/problem+json`, with `status` 500, a `title` and a `traceId`. The body contains neither the marker nor the exception's type name nor a stack trace. Exactly one entry at `Error` across all categories, carrying that same exception. No `AppointmentBooked` or `BookingRejected` entry (`api-conventions.md`, *Errors*; *Exceptions vs results*) |
| EC-76 | Technical guard: the body is not valid JSON | `400` and no entry at `Error`. The test host runs in `Development`, where minimal APIs throw `BadHttpRequestException` for a bad body, and a global handler would turn that into a `500`. The behaviour must be the same in every environment (`api-conventions.md`, status table). EC-30 and the other shape errors stay in the next branch |

Entries are read by their `EventId.Name` and level, and by the key-value pairs of their state. The `EventId` names and the
value names above are the log contract; `Reason` holds the error's `Code`.

## Test setup

- **Hand-written capturing logger** in `tests/ServiceScheduler.IntegrationTests/Fixtures/`: an `ILoggerProvider` that records category,
  level, `EventId`, formatted message, state key-value pairs and exception in a thread-safe collection. No new package. The fixture owns one
  instance, exposes it and registers it in `ConfigureTestServices`.
- **Data reset:** a fixture method that runs `DELETE FROM appointments` directly over Npgsql, keeps the seed rows and clears the captured logs.
  `PostAppointmentTests` implements `IAsyncLifetime` and calls it before each test. This is the reset `step-4-booking-flow.md` asks the first
  follow-up to settle.
- **New tests go in `PostAppointmentTests`**, so they share one container. The EC-72 test method does not change by a single character; only the
  class declaration gains `IAsyncLifetime`. The developer approved this change to a frozen test on 2026-11-02.
- **EC-75:** a hand-written `ISchedulingReader` that throws, swapped in with `fixture.WithWebHostBuilder(... ConfigureTestServices ...)`.
- Expect the `Development` environment that `WebApplicationFactory` uses: EC-75 and EC-76 must hold there.

## Code shape

- **Api only.** Logging lives in the endpoint, because Application has no logging reference and no new package is approved. Do not reference
  a logging package from Domain or Application.
- `[LoggerMessage]` methods define the two business events, with their `EventId` names. No string interpolation.
- One `IExceptionHandler`, registered with `AddExceptionHandler`, plus `UseExceptionHandler()`. It logs once at `Error` and writes the
  ProblemDetails through `IProblemDetailsService`. It never returns the exception's message, type or stack trace.
- Check that the framework does not also log the handled exception at `Error`; EC-75 counts entries across all categories.

## Approved extras

No NuGet package and no change to the public API contract. The `500` is already the documented behaviour. If either seems necessary, stop and report.

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
8. Bad-request exceptions are no longer kept out of the global handler (EC-76).
