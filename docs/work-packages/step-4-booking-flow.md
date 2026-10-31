# Work package: step 4, the end-to-end booking flow (straight path)

- **Branch:** `feature/allocation` (one pull request once this flow works)
- **Status:** scenario, database design and packages approved by the developer on 2026-10-31
- **Rules:** `.claude/rules/booking-domain.md`, `persistence.md`, `api-conventions.md`; ADR-0001; ADR-0002 #22–25 and *Delivery order*
- **Design:** `docs/system-design.md` §5, §6, §7.1, §9

## Scope

One straight path, end to end, against real PostgreSQL:

1. An advisor sends `POST /api/appointments` with customer, vehicle, service type, dealership and start time.
2. The system loads the booking context and the confirmed appointments from the database.
3. It runs `BookingValidator`, then `SlotAllocator`.
4. It saves the appointment.
5. It returns `201` with `Location` and the confirmed appointment.

**Out of scope, do not build:** `Idempotency-Key`, cancellation, `GET` endpoints, availability, alternatives, the
booking horizon, buffers beyond storing `buffer_minutes` and `blocked_until`, translating `23P01`, allocation retry,
rule 8 (vehicle overlap in code), Docker Compose, OpenTelemetry, health checks, OpenAPI document. Leave
`BookingOptions` out until something reads it.

## Scenario (the only test)

| EC | Scenario | Expected (source) |
|---|---|---|
| EC-72 | Seed data: a dealership open on Tuesday, a `{General}` technician on shift, a `Standard` bay, a customer who owns a vehicle, an oil change service type. The advisor books that vehicle at 10:00 on a Tuesday | `201` with `Location`; body has customer, vehicle, service type, dealership, technician and bay IDs, start and end, status `Confirmed`. Exactly one row in `appointments` with those same values (R1–R3 in `docs/system-design.md`) |

Assert through the real HTTP pipeline (`WebApplicationFactory`) and read the row back from the database directly.

## Database: the first migration

Exactly `docs/system-design.md` §6, read including its design notes:

- All ten tables; every foreign key `ON DELETE RESTRICT`, including the composite ones for technician and bay.
- All `CHECK` constraints, and the three exclusion constraints with `btree_gist`, through `migrationBuilder.Sql` with a matching `Down`.
- `ix_appointments_dealership_start`. Not: `idempotency_key`, `request_hash`, `cancelled_at`, `ux_appointments_idempotency_key`.
- Tables and columns in snake_case. Seed data is deterministic and idempotent, with readable fixed IDs.

### Seed data (the frozen EC-72 test depends on these values)

The IDs are the constants in `src/ServiceScheduler.Infrastructure/Persistence/SeedData.cs`. Seed exactly this, nothing for later branches:

| Row | Values |
|---|---|
| Dealership | Open Tuesday 08:00–17:00 |
| Technician | One, active, belongs to the dealership, skill `General`, Tuesday shift 08:00–17:00 |
| Service bay | One, active, belongs to the dealership, type `Standard` |
| Customer and vehicle | One customer who owns the one vehicle, with a valid 17-character VIN and placeholder personal data |
| Service type | Oil change: 60 minutes, buffer 0, required skill `General`, required bay type `Standard` |
| Appointments | None |

## Code shape

- **Domain:** `Appointment` with `Appointment.Book(...)`; `BlockedUntil` equals the end while buffers are 0. Keep `DateTime` values `Unspecified`.
- **Application:** `BookAppointmentCommand`, `BookAppointmentHandler`, `ISchedulingReader`, `IAppointmentRepository` with
  only the members this path uses. Busy intervals cover the technicians' and bays' **whole date**, because workload
  needs it (`docs/edge-cases.md`, *Deferred*).
- **Infrastructure:** `SchedulingDbContext`, configurations, the migration, the reader and repository. Keep the Domain free of EF Core.
- **Api:** one endpoint in a `/api` route group, request and response contracts. Expected failures from the handler map to
  `ProblemDetails` by `ErrorKind` in one place (unknown references are `NotFound`). These mappings get their tests with the branches.
- `TimeProvider` is injected. Tests replace it with a small fixed subclass written in the test project.

## Approved extras

NuGet packages approved by the developer: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`,
`Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing`, on current stable versions compatible with .NET 10.
Nothing else. If another package seems necessary, stop and report. The `POST` contract is the one in §9, without the idempotency header.

## Mutations

One, added back after the time-zone removal took the old one away: **"Workload counts every date"**, in `SlotAllocator`, the
technician's workload no longer filtered by the requested date. `tools/mutation/mutate.ps1` runs the unit tests only, so EC-72
has no mutation. Existing mutations must still pass; update one only if the code it targets moved.

## Follow-ups after this path works

The branches, one at a time, each shown to the developer first: invalid request, no capacity, two technicians and two bays,
same vehicle twice, back-to-back, EV with no EV resource, concurrent requests. Invalid request comes first because review
confirmed EC-30 books the wrong time today.

"Invalid request" must include EC-30 (an offset on `requestedStart` is a `400`, ADR-0002 #25): the plain `DateTime` contract
accepts `+03:00` and converts it, booking the wrong time with a `201`. Past start must include a date earlier than today
(EC-09), which no test covers since the time-zone tests were removed.

The first follow-up package must also settle two things. EC-72 asserts one row in the whole table and shares its database
through `IClassFixture`, so each test needs a data reset that keeps the seed rows (`testing.md`). EC-72 also relies on a single
seeded technician and bay, so later branches add their extra resources in their own setup, not in the migration seed.
