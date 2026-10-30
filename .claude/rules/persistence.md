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

- Columns for instants are `timestamptz` and hold UTC. Map `DateTimeOffset` with offset zero.
- Double-booking is prevented by exclusion constraints (requires `btree_gist`), created in a migration:

  ```sql
  ALTER TABLE appointments ADD CONSTRAINT ex_appointments_technician_overlap
    EXCLUDE USING gist (technician_id WITH =, tstzrange(start_utc, blocked_until_utc, '[)') WITH &&)
    WHERE (status = 'Confirmed');
  -- same for service_bay_id; vehicle_id uses tstzrange(start_utc, end_utc) (buffer does not hold the vehicle)
  ```
- Tenant boundaries are enforced by composite foreign keys (`(technician_id, dealership_id)` →
  `technicians (id, dealership_id)`), not only by application code.
- Reference ranges carry `CHECK (closes_at > opens_at)`; PostgreSQL `time` accepts `24:00`, .NET `TimeOnly` does not.
- Translate `PostgresException` with `SqlState == PostgresErrorCodes.ExclusionViolation` (`23P01`) to a
  domain error in Infrastructure; constraint name tells which resource conflicted. Never let a raw
  provider exception cross into Application.
- State transitions: prefer one atomic conditional update (`ExecuteUpdateAsync` with the expected state
  in `WHERE`, check rows affected). Use PostgreSQL `xmin` as a row version only when an aggregate is
  loaded, changed in memory and saved.
- Every foreign key and every column used in a `WHERE`/`JOIN` on a hot path has an index. The GiST
  index behind each exclusion constraint also serves overlap queries.
- Naming: snake_case tables and columns; constraint names are explicit and descriptive.

## Migrations

- One migration per logical change, with a descriptive name. Review the generated SQL before applying.
- Never edit a migration that has been applied/committed; add a new one.
- Raw SQL in migrations (extensions, constraints) goes through `migrationBuilder.Sql` with a matching `Down`.
- Seed data for the demo is deterministic and idempotent.

## Safety

- Raw SQL only via parameterised APIs (`FromSql`, `ExecuteSql` with interpolated parameters). Never
  build SQL with string concatenation.
