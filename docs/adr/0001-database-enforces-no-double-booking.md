# ADR-0001: The database enforces "no double-booking"

- **Status:** Proposed
- **Date:** 2026-10-30

## Context

A booking needs a technician and a bay that are both free for the whole interval. The naive flow,
"query availability, then insert", has a race: two concurrent requests can both observe the slot as
free and both insert, double-booking a technician or bay. The service must also stay correct when
several API instances run behind a load balancer.

## Decision

PostgreSQL **exclusion constraints** (with `btree_gist`) on `appointments` reject any two confirmed
rows that overlap (half-open `'[)'` ranges) for:

- the same `technician_id` or `service_bay_id`, over `[start_utc, blocked_until_utc)` (service + buffer);
- the same `vehicle_id`, over `[start_utc, end_utc)` (service only).

The application still pre-filters candidates to choose resources and produce precise errors. On a
`23P01` violation for a technician or bay it retries allocation with the next candidate (max 3
attempts), then returns `409`; a vehicle violation returns `409 VehicleAlreadyBooked` without retry.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| In-process `lock` / `SemaphoreSlim` | Only protects one process; breaks with two instances |
| `SERIALIZABLE` isolation + retry | Correct, but every booking path must handle serialization failures, and correctness depends on every query being written correctly |
| `SELECT ... FOR UPDATE` on technician/bay rows | Works, but locks must be taken in a consistent order to avoid deadlocks and is easy to bypass in future code |
| Pre-generated slot table with unique constraint | Forces a fixed slot length; poor fit for services of different durations |

## Consequences

- Correctness holds regardless of instance count or future code paths: the rule lives with the data.
- Requires PostgreSQL (`btree_gist`); integration tests must run against real PostgreSQL (Testcontainers).
- Infrastructure must translate `23P01` into a domain error, using the constraint name to identify the resource.
- The GiST indexes created by the constraints also speed up availability (overlap) queries.

## Acceptance

Accept when the concurrency test (EC-17) passes against real PostgreSQL.

## Later enhancements (deferred)

- Add advisory locks per dealership-day and the SQL Server fallback to *Alternatives considered*.
- List the downsides: hot-spot contention; new resource-occupying statuses must join the constraints' `WHERE`; error mapping depends on constraint names.
- Negative control: the EC-17 test must fail when the constraint is dropped.
