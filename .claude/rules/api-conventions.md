---
paths:
  - "src/ServiceScheduler.Api/**"
  - "tests/ServiceScheduler.IntegrationTests/**"
---

# API conventions

## Resources and status codes

| Method & route | Success | Expected failures |
|---|---|---|
| `POST /api/appointments` | `201 Created` + `Location` | `400` malformed body, `404` unknown reference, `409` no capacity / vehicle already booked, `422` business validation |
| `GET /api/appointments/{id}` | `200` | `404` |
| `POST /api/appointments/{id}/cancel` | `200` | `404`, `409` invalid state transition |
| `GET /api/dealerships/{id}/availability?serviceTypeId=&date=` | `200` | `404`, `400` |
| `GET /health/live`, `GET /health/ready` | `200` | `503` when dependencies are down |

- Plural nouns, kebab-case paths, camelCase JSON. Actions that are not CRUD use a sub-resource verb
  (`/cancel`), never a `PATCH` with a magic status value.
- Timestamps in contracts are ISO-8601 with offset (`2026-11-03T09:00:00+00:00`); responses return UTC
  plus the dealership's local time where useful for display.
- Version the API from day one (`/api/...` maps to v1; breaking changes require a new version).

## Contracts

- Request/response records live in the Api project. Never serialise domain entities or EF types.
- Validate **shape** at the edge (required fields, formats) with .NET's built-in minimal-API validation;
  **business** validation belongs to the domain. Do not duplicate rules across both.
- Use `TypedResults` so OpenAPI metadata is accurate. Every endpoint declares its success and error responses.

## Errors

- All errors are RFC 9457 `application/problem+json` with a stable machine-readable `type`/`code`
  (e.g. `no-qualified-technician-available`), a human `title`, `traceId`, and, for capacity conflicts,
  suggested alternative start times.
- Domain errors map to status codes in one place (a single mapper); handlers never pick status codes.
- Unhandled exceptions are converted by the global `IExceptionHandler` to a generic 500 ProblemDetails.
  Stack traces and exception messages are never returned to clients.

## Idempotency

`POST /api/appointments` honours `Idempotency-Key` (see `booking-domain.md`). Keys are stored with a
hash of the request payload; a replay returns the original response.

## Security baseline

- No secrets in source or `appsettings.json`; use environment variables / user-secrets.
- Request size limits stay at framework defaults or lower; reject unknown JSON properties.
- Personal data (customer name, phone, email) is never written to logs.
- Authentication/authorisation are out of scope for the assessment but endpoints are grouped so a
  policy can be applied in one place (`MapGroup("/api").RequireAuthorization()`).
