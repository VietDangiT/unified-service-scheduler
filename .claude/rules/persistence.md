---
paths:
  - "src/ServiceScheduler.Infrastructure/**"
  - "tests/ServiceScheduler.IntegrationTests/**"
---

# Persistence: EF Core + PostgreSQL

## Queries and the N+1 problem

- Never query inside a loop. Load what a use case needs in one round trip (or a fixed, small number).
  If you write `foreach (...) { await db.X.Where(...)... }`, stop and restructure.
- Lazy loading stays disabled. Load related data explicitly with `Include` or, preferably, a `Select`
  projection to exactly the columns the use case needs.
- Multiple collection `Include`s on one query cause cartesian explosion; use `AsSplitQuery()` or separate projections.
- Read paths use `AsNoTracking()`. Tracking is only for aggregates that are about to be modified.
- Availability is computed set-based in SQL (one query returning busy intervals for the candidate
  resources in the window), not by loading appointments and filtering in memory.
- Any list endpoint is bounded (paging or a date window). No unbounded `ToListAsync()` on growing tables.
- Do not return `IQueryable` from repositories; return materialised results or domain objects.
- Inspect generated SQL (`ToQueryString()` or logging at Debug) for every non-trivial query you add.

## Connections and DbContext

- Npgsql pools connections by default; never open `NpgsqlConnection` manually unless there is a reason, and
  always `await using` it.
- Register with `AddDbContextPool` (or `AddDbContext`) from the composition root; the connection string
  comes from configuration / environment, never from code.
- Keep transactions short. No network calls, no waiting on external systems, no user think-time inside a transaction.
- If `EnableRetryOnFailure` is enabled, wrap explicit transactions in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- A retried write may already have committed (acknowledgement lost). Generate keys in the application
  (`Guid.CreateVersion7()`) and, on an unknown outcome, check whether the row exists before reporting failure.

## Schema and constraints

- Columns for times are `timestamp` (no zone), mapped to `DateTime` (ADR-0002 #25).
- Double-booking is prevented by exclusion constraints (requires `btree_gist`), created in a migration:

  ```sql
  ALTER TABLE appointments ADD CONSTRAINT ex_appointments_technician_overlap
    EXCLUDE USING gist (technician_id WITH =, tsrange(start_at, blocked_until, '[)') WITH &&)
    WHERE (status = 'Confirmed');
  -- same for service_bay_id; vehicle_id uses tsrange(start_at, end_at) (buffer does not hold the vehicle)
  ```
- Tenant boundaries are enforced by composite foreign keys (`(technician_id, dealership_id)` →
  `technicians (id, dealership_id)`), not only by application code.
- Reference ranges carry `CHECK (closes_at > opens_at AND closes_at < '24:00')`; PostgreSQL `time` accepts `24:00`, .NET `TimeOnly` does not.
- Translate `PostgresException` with `SqlState == PostgresErrorCodes.ExclusionViolation` (`23P01`) to a
  domain error in Infrastructure; constraint name tells which resource conflicted. Never let a raw
  provider exception cross into Application.
- State transitions: prefer one atomic conditional update (`ExecuteUpdateAsync` with the expected state
  in `WHERE`, check rows affected). Use PostgreSQL `xmin` as a row version only when an aggregate is
  loaded, changed in memory and saved.
- Every column used in a `WHERE`/`JOIN` on a hot path has an index, foreign keys included when they are on
  one. The GiST index behind each exclusion constraint also serves overlap queries.
- Foreign keys are `ON DELETE RESTRICT`; never rely on EF Core's default cascade for required relationships.
- Naming: snake_case tables and columns; constraint names are explicit and descriptive.

## Migrations

- One migration per logical change, with a descriptive name. Review the generated SQL before applying.
- Never edit a migration that has been applied/committed; add a new one.
- Raw SQL in migrations (extensions, constraints) goes through `migrationBuilder.Sql` with a matching `Down`.
- Seed data for the demo is deterministic and idempotent.

## Safety

- Raw SQL only via parameterised APIs (`FromSql`, `ExecuteSql` with interpolated parameters). Never
  build SQL with string concatenation.
