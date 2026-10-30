---
paths:
  - "src/**/*.cs"
  - "tests/ServiceScheduler.IntegrationTests/**"
---

# Async, threading and concurrency

## async/await

- Async all the way for I/O. Never block on async code: no `.Result`, `.Wait()`,
  `.GetAwaiter().GetResult()`. They cause thread-pool starvation under load.
- Every async method that does I/O accepts a `CancellationToken` as its last parameter and passes it
  to every downstream call (EF Core, `HttpClient`, `Task.Delay`). Endpoints bind it from `HttpContext.RequestAborted`.
- No `async void` (except event handlers, which this project has none of).
- Return `Task`/`Task<T>`; use `ValueTask` only when measured hot paths complete synchronously.
- Do not wrap synchronous code in `Task.Run` inside request handling; it only moves work to another
  pool thread. `Task.Run` is for CPU-bound work off a UI thread, which we do not have.
- `ConfigureAwait(false)` is not needed in ASP.NET Core application code (no synchronization context).
- Name async methods with the `Async` suffix.
- No fire-and-forget work in a request. Background work goes through a hosted service
  (`BackgroundService` + `Channel<T>`) and resolves scoped services via `IServiceScopeFactory`.

## Thread safety and DI lifetimes

- `DbContext` is **not thread-safe**: one context per request scope; never share it across concurrent
  operations, never `Task.WhenAll` two queries on the same context.
- Singletons must be stateless or explicitly thread-safe. Never inject a scoped service into a
  singleton (captive dependency). `ValidateScopes`/`ValidateOnBuild` stay enabled.
- No mutable `static` state. Caches use `IMemoryCache`/`HybridCache` with explicit expiry.
- `lock` never spans an `await` (it cannot compile); if async mutual exclusion is truly needed in-process,
  use `SemaphoreSlim`. **In-process locks are never a correctness guard for shared data**; the service
  must stay correct with N instances behind a load balancer.

## Race conditions: how this service stays correct

| Race | Guard |
|---|---|
| Two bookings for the same technician/bay/vehicle and time | PostgreSQL exclusion constraint (`EXCLUDE USING gist ... WITH &&`). Violation `23P01` is translated to a domain error, then allocation retries with the next candidate |
| Client retries a POST after a timeout | `Idempotency-Key` with a unique index on the key |
| Two writers changing the same appointment (e.g. two cancels) | Atomic conditional update (`WHERE status = 'Confirmed'`); 0 rows affected → reload and decide. `xmin` row version when an aggregate is loaded, modified and saved |
| Commit succeeded but the acknowledgement was lost; the retry runs again | App-generated ID; check whether the row exists before reporting a conflict |
| "Check then act" in application code | Allowed only as a pre-filter; the database constraint is the decision |

Never "fix" a race with `lock`, `SemaphoreSlim`, a `static` flag, `Thread.Sleep` or a longer timeout.

## Time

Inject `TimeProvider`; never call `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.Now` directly.
Tests use `FakeTimeProvider`. The Domain stays pure: domain methods take `DateTimeOffset now` as a
parameter, and Application supplies `timeProvider.GetUtcNow()`; domain tests pass fixed instants.
