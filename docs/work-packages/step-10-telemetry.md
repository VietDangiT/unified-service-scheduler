# Work package: step 10, metrics and tracing with OpenTelemetry

- **Branch:** `feature/allocation`, adding to PR #2
- **Status:** the developer approved the plan and the four NuGet packages on 2026-11-05
- **Rules:** `.claude/rules/errors-logging-observability.md` (*Tracing and metrics*); `docs/system-design.md` §11

## Scope

1. **Metrics.** One `Meter` named `ServiceScheduler`. Tags have low cardinality: no customer, appointment or vehicle IDs, and no timestamps.

| Instrument | Kind | Tags | Recorded when |
|---|---|---|---|
| `scheduler.bookings.created` | counter | `service_type` (the service type's code, carried on `BookingContext`), `dealership_id` | A booking is saved |
| `scheduler.bookings.rejected` | counter | `reason` (the error code, for example `OutsideOpeningHours`) | A booking request ends with a domain error |
| `scheduler.booking.allocation_retries` | counter | none | Each retry after a lost race (step 9) |
| `scheduler.booking.duration` | histogram, ms | `outcome` (`created` / `rejected`) | Once per booking request |

2. **Tracing.** One `ActivitySource` named `ServiceScheduler`, with spans `booking.allocate` (tags `attempt`, `outcome`) and
   `booking.persist` (tags `attempt`, `outcome`: `added`, `conflict:technician`, `conflict:bay`, `conflict:vehicle`). Add ASP.NET Core
   and Npgsql instrumentation.
3. **Export.** Logs, traces and metrics go out over OTLP. The endpoint comes from configuration (`OTEL_EXPORTER_OTLP_ENDPOINT`); when it
   is not set, nothing is exported, so tests and local runs need no collector. `docker-compose.yml` adds the Aspire Dashboard
   (`mcr.microsoft.com/dotnet/aspire-dashboard`, pinned tag, UI on `127.0.0.1:18888`, OTLP on the internal network). The API points at it.
   Set the service name `service-scheduler`.

`System.Diagnostics` (`Meter`, `ActivitySource`) is part of the BCL, so the Application layer may record metrics and spans without a
package. Domain stays free of them. NuGet packages go into the Api project only.

## Approved packages (Api project)

- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `Npgsql.OpenTelemetry`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol`

Use the latest stable versions that restore with no vulnerability warning. Warnings are errors; never suppress NU1903.

## Scenarios (tests with the BCL `MeterListener` and `ActivityListener`; no test package)

| EC | Given / When | Then |
|---|---|---|
| EC-92 | A free technician and bay; a booking | `bookings.created` +1 with `service_type`; `booking.duration` recorded once with `outcome=created`; no `bookings.rejected` |
| EC-93 | A request on a closed day | `bookings.rejected` +1 with `reason=OutsideOpeningHours`; `booking.duration` once with `outcome=rejected`; no `bookings.created` |
| EC-94 | The first save loses the race (unit, fake repository, as EC-18) | `allocation_retries` +1; with every save losing (as EC-44), +2 |
| EC-96 | One lost race on the technician, then success (unit) | Two `booking.allocate` spans; `booking.persist` outcomes `conflict:technician`, then `added` |
| EC-95 | A successful booking | Exactly one `booking.allocate` span and one `booking.persist` span with `outcome=added`, both children of the request's span |

Listeners must filter on the `ServiceScheduler` meter and source, and tests must not interfere with each other. Tests run in parallel
across classes, so a test counts only measurements it can attribute to itself, for example by tag or by a dealership it created, or the
listener class is put in a non-parallel collection.

## Mutations

1. `bookings.created` not recorded → EC-92
2. Rejection reason recorded as a fixed text → EC-93
3. Retry counter not incremented → EC-94
4. `booking.persist` outcome always `added` → EC-96

## Done when

- Build, unit, integration and format pass.
- `docker compose up --build`, one booking, then the dashboard at `http://localhost:18888` shows the trace (HTTP → booking.allocate →
  booking.persist → SQL) and the metrics. Checked by the implementer and described in its report.

## Results (2026-11-05)

**Red** (re-run by the orchestrator). Unit: 87/90. Integration: 56/59. All six new tests failed because nothing was recorded. `domain-analyst`:
ready. Fingerprint `4f32a5dd`.

**Green** (re-run by the orchestrator): build clean, 90/90 unit, 59/59 integration, format clean, `mutate.ps1` 35/35.

Packages: `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore` and `OpenTelemetry.Exporter.OpenTelemetryProtocol`
1.20.1; `Npgsql.OpenTelemetry` 10.0.3. No vulnerable packages. Dashboard image `mcr.microsoft.com/dotnet/aspire-dashboard:13.6.2`.

| Mutation | Run | Killed by |
|---|---|---|
| 1. `bookings.created` not recorded | By hand, integration | EC-92 |
| 2. Rejection reason as fixed text | By hand, integration | EC-93 |
| 3. Retry counter not incremented | `mutate.ps1` | EC-94 |
| 4. Persist outcome always `added` | `mutate.ps1` | EC-96 |

**Demo** (implementer). The dashboard received the `service-scheduler` resource with logs, traces and metrics. The trace was
`POST /api/appointments` → `booking.allocate` (attempt 1, allocated) → SQL, and `booking.persist` (attempt 1, added) → the lock and insert
SQL. The logs carried the trace IDs.
