---
paths:
  - "src/ServiceScheduler.Domain/**"
  - "src/ServiceScheduler.Application/**"
  - "tests/**"
---

# Booking domain rules

Source of truth for business behaviour. If code and this file disagree, stop and ask.
Status of each assumption is tracked in `docs/adr/`. Every rule maps to a row in `docs/edge-cases.md`.

## Booking request

Input: `customerId`, `vehicleId`, `serviceTypeId`, `dealershipId`, `requestedStart` (ISO-8601 date and
time, **no** offset: the client always sends a plain time, and an offset is out of scope and not checked, ADR-0002 #25),
optional `Idempotency-Key` header. The caller is a service advisor using an internal tool (no end-customer auth in scope).

Shape errors are `400` (API layer): missing fields, malformed IDs, `Idempotency-Key` empty or longer than 64 characters.

## Validation (reject with a domain error, never an exception)

Evaluated **in this order**; the first failure is returned, so the same input always yields the same error.

| # | Rule | Error |
|---|---|---|
| 1 | Referenced customer, vehicle, service type or dealership does not exist | `NotFound` |
| 2 | Vehicle does not belong to the customer | `VehicleNotOwnedByCustomer` |
| 3 | Dealership has no active compatible bay, or no active technician with the required skill, at all (a static capability check: shifts and bookings are not considered, ADR-0002 #19) | `ServiceNotOfferedAtDealership` |
| 4 | `requestedStart` is not after now (per `TimeProvider`). No minimum notice (ADR-0002 #14) | `StartInPast` |
| 5 | `requestedStart` is beyond the booking horizon (`BookingHorizonDays`, default 90) | `BeyondBookingHorizon` |
| 6 | `requestedStart` is not on the 15-minute grid of the clock (seconds and sub-seconds must be zero) | `StartNotOnGrid` |
| 7 | The **service** interval `[start, start + duration)` is not inside the opening hours of that date (no row for the weekday = closed) | `OutsideOpeningHours` |
| 8 | The vehicle has an overlapping confirmed appointment at **any** dealership | `VehicleAlreadyBooked` |
| 9 | No eligible technician / bay for the interval; when **both** are missing, the technician error is returned (ADR-0002 #17) | `NoQualifiedTechnicianAvailable` / `NoCompatibleBayAvailable` |

## Intervals: service vs blocked

- **Service interval** `[start, serviceEnd)`, `serviceEnd = start + serviceType.Duration`. Must fit opening
  hours and the technician's shift. The vehicle is occupied for this interval only.
- **Blocked interval** `[start, blockedUntil)`, `blockedUntil = serviceEnd + serviceType.Buffer` (default 0).
  The technician and bay are occupied for this interval. The buffer **may run past closing time or the
  end of the shift** (clean-up after the last job).
- Both times are stored on the appointment; later changes to a service type never alter existing appointments.
- A duration that is not a multiple of 15 minutes is allowed; the resource is next bookable at the
  first grid point at or after `blockedUntil`.

## Capacity rules

- **Technician is eligible** when: belongs to the dealership, is active, holds `serviceType.RequiredSkill`,
  has a shift covering the whole **service** interval, and has no confirmed appointment whose blocked
  interval overlaps the requested blocked interval.
- **Bay is eligible** when: belongs to the dealership, is active, its `BayType` satisfies
  `serviceType.RequiredBayType`, and has no overlapping confirmed blocked interval.
  Compatibility: a `Standard` job can use any bay type; `Lift`, `EV` and `Alignment` jobs need a bay of that exact type.
- "Belongs to the dealership" holds by construction: the `Dealership` aggregate rejects a technician or
  bay of another dealership (ADR-0002 #21), so rule 3 and the allocator do not filter for it.
- The job must fit inside **both** opening hours and the shift; a shift that extends beyond opening hours
  does not extend the bookable window.

## Overlap semantics

Half-open intervals: `a.Start < b.End && b.Start < a.End`. Back-to-back (`10:00–11:00`, `11:00–12:00`)
does **not** overlap. Only `Confirmed` appointments count.

## Allocation policy (`SlotAllocator`)

Deterministic, so tests and demos are reproducible. Both policies are **best fit**: never spend a scarce
resource on a job that a less specialised one can do.

1. **Bay: best fit.** Take the least specialised compatible free bay, in the fixed order
   `Standard < Lift < Alignment < EV` (ADR-0002 #18): a `Standard` job uses a `Standard` bay, then
   `Lift`, then `Alignment`, and `EV` only as a last resort. Tie-break by bay ID.
2. **Technician: best fit, then least loaded.** Fewest skills held, counting every skill (protects
   specialists; `{General, EV}` and `{General, Alignment}` tie); then fewest blocked minutes (service +
   buffer) of `Confirmed` appointments on that date (ADR-0002 #20); then technician ID.
   Skills match exactly: a technician needs the required skill itself.
3. If persisting fails with an exclusion violation on a technician or bay (a concurrent request won
   the race), exclude that resource and retry allocation, at most `MaxAllocationAttempts` (3) in total.
   When attempts run out, return the capacity error for the resource that conflicted last.
   PostgreSQL reports one violated constraint at a time; a second conflict surfaces on the next attempt.
4. A violation on the **vehicle** constraint is not retried: return `VehicleAlreadyBooked`.
5. A capacity error (`NoQualified…`, `NoCompatible…`) includes up to `AlternativesToSuggest` (3) start
   times: the earliest free starts **after** the requested start, within `AlternativesSearchDays` (7)
   days, never in the past, and not overlapping the vehicle's other bookings. The list may be empty.

## Time and calendars

- One clock for everything: a time is a plain date and time, with no time zone and no UTC conversion
  (ADR-0002 #25). `10:00` means 10:00 in the request, the response and the database.
- Opening hours and shifts are one `[opens, closes)` range per weekday.
- `closes > opens` on the same date: no `24:00`, no ranges crossing midnight, no split shifts or lunch
  breaks. Appointments therefore never span midnight.
- "Now" comes from the server clock through `TimeProvider`, so that clock must be set to the dealership's time.

## Idempotency

`POST /api/appointments` accepts an `Idempotency-Key`; keys are global.

- Same key, same payload (`request_hash`) → return the stored appointment **in its current state**
  (including `Cancelled`).
- Same key, different payload → `IdempotencyKeyReused` (422).
- Only successful bookings are stored. A key whose first attempt was rejected is evaluated afresh.
- The appointment ID is generated by the application (`Guid.CreateVersion7()`) before persisting. If a
  write fails after a possible commit (connection lost), check whether that ID exists before reporting failure.

## Cancellation

The only status transition is `Confirmed → Cancelled` (atomic conditional update).

| Current state | Result |
|---|---|
| `Confirmed`, start in the future | `200`, status `Cancelled`, `cancelledAt` set; the slot is immediately bookable |
| Already `Cancelled` | `200`, unchanged (idempotent) |
| `Confirmed`, start ≤ now | `AppointmentAlreadyStarted` (409) |
| Unknown ID | `NotFound` (404) |

## Availability query

Past dates and dates beyond the booking horizon → `422`. For today,
start times at or before now are excluded. Unknown dealership or service type → `404`.

## Out of scope (documented, not implemented)

Authentication, notifications, payments, rescheduling, multi-technician jobs, multi-day jobs,
technician leave or sickness, public holidays and one-off closures, split shifts and lunch breaks,
`Completed` / no-show statuses, customer self-service portal, authorised drivers for company/lease/fleet
vehicles (ADR-0002 #16), managing resources and technician job views (seeded data, ADR-0002 #22).

Rules above that are parked until the core works are listed in ADR-0002, *Delivery order*.
