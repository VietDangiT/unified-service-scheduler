---
paths:
  - "src/**/*.cs"
---

# Errors, logging and observability

## Exceptions vs results

- **Expected business outcomes** (no capacity, validation failure, not found) are returned as
  `Result<T>` with a typed domain error. They are not exceptional and must not use exceptions for control flow.
- **Exceptions** are for broken assumptions and infrastructure failures (database down, bug).
- Catch only what you can handle meaningfully, as close as possible to where it is thrown
  (e.g. translating `23P01` in Infrastructure). Never `catch (Exception)` except in the global handler
  or a top-level background loop. Never swallow an exception; never `throw ex;` (use `throw;`).
- Throw specific exception types; include context in the message, not personal data.
- One global `IExceptionHandler` turns unhandled exceptions into a 500 ProblemDetails and logs once at `Error`.

## Structured logging

- Use `ILogger<T>` with **message templates**, never string interpolation:
  `logger.LogInformation("Appointment {AppointmentId} booked for {DealershipId}", id, dealershipId);`
- Hot paths and frequently logged events use the `[LoggerMessage]` source generator.
- Levels: `Debug` diagnostics; `Information` business events (booked, cancelled); `Warning` handled
  anomalies (allocation retry after a constraint violation); `Error` failures needing attention.
  Expected business rejections (409/422) are `Information`, not `Error`.
- Log once per failure, at the boundary that handles it. Do not log-and-rethrow at every layer.
- Every log carries the trace/correlation ID (automatic via OpenTelemetry + logging scopes).
- Never log personal data, connection strings, tokens or full request bodies. VINs may be logged.
- Output JSON in containers (`AddJsonConsole` or OTLP exporter); human-readable console in Development.

## Tracing and metrics (OpenTelemetry)

- Instrument ASP.NET Core, HttpClient and Npgsql/EF Core; export via OTLP (Aspire dashboard locally).
- One `ActivitySource` and one `Meter`, both named `ServiceScheduler`. Add spans only around meaningful
  units of work (allocation, persistence retry), with low-cardinality tags.
- Business metrics:
  - `scheduler.bookings.created` (counter; tags: `dealership_id`, `service_type`)
  - `scheduler.bookings.rejected` (counter; tag: `reason`, e.g. `no_technician`, `no_bay`, `outside_hours`)
  - `scheduler.booking.allocation_retries` (counter): non-zero shows real contention
  - `scheduler.booking.duration` (histogram, ms)
- Tags never contain unbounded values (IDs of customers, appointment IDs, timestamps).
- Health checks: `/health/live` (process up) and `/health/ready` (database reachable).
