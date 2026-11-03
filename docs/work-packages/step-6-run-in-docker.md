# Work package: step 6, the app runs with one command

- **Branch:** its own worktree, merged after step 5 is committed
- **Status:** started 2026-11-02 at the developer's request to run work in parallel
- **Source:** the brief asks for a runnable service with build, run and test instructions (README is the orchestrator's job)

## Scope

`docker compose up --build` from the repo root starts PostgreSQL 16 and the API. The API applies the migrations, which include the demo
seed, and answers `POST /api/appointments`.

1. `src/ServiceScheduler.Api/Dockerfile`: multi-stage (`mcr.microsoft.com/dotnet/sdk:10.0` build, `aspnet:10.0` runtime), runs as non-root,
   listens on 8080.
2. `docker-compose.yml` at the root:
   - `postgres:16` with a `pg_isready` healthcheck and a named volume; no fixed host port, or 5433 if one is needed.
   - `api` depends on `postgres` being healthy, is published on `8080`, and gets `ConnectionStrings__Scheduling` from the environment.
     The password is a local demo value in the compose file, never in `appsettings.json`.
   - The compose `TZ` defaults to `UTC` and can be overridden. "Now" is the server clock (`booking-domain.md`, *Time and calendars*).
3. Migrations on startup: behind a configuration flag `Database:ApplyMigrationsOnStartup` (default `false`, `true` in compose). It calls
   `Database.MigrateAsync()` once before `app.Run()`, in a scope. Integration tests keep their own migration path, so they are unaffected.
4. `src/ServiceScheduler.Api/ServiceScheduler.Api.http`: a successful booking with the seeded IDs, the same request again (`409`), and an
   unknown ID (`404`). The base URL is `http://localhost:8080`. Use a requested start that is in the future when the developer runs it,
   on a weekday the seed has open, and say in a comment which day that is.

**Out of scope:** health endpoints that need a NuGet package, the Aspire dashboard and OpenTelemetry (step 4 logging), and the README.

## Approved extras

No NuGet package. No change to the public API contract. If either seems necessary, stop and report.

## Done when

- `docker compose up --build -d` runs, then the three `.http` requests return `201`, `409` and `404`, checked with `curl`.
- `docker compose down -v` cleans up.
- The build, unit, integration and format gate still passes.
