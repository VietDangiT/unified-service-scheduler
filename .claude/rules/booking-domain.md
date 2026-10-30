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

Input: `customerId`, `vehicleId`, `serviceTypeId`, `dealershipId`, `requestedStart` (ISO-8601 **with**
offset), optional `Idempotency-Key` header. The caller is a service advisor using an internal tool
(no end-customer auth in scope).

Shape errors are `400` (API layer): missing fields, malformed IDs, `requestedStart` without an offset,
`Idempotency-Key` empty or longer than 64 characters.

## Validation (reject with a domain error, never an exception)

Evaluated **in this order**; the first failure is returned, so the same input always yields the same error.

| # | Rule | Error |
|---|---|---|
| 1 | Referenced customer, vehicle, service type or dealership does not exist | `NotFound` |
| 2 | Vehicle does not belong to the customer | `VehicleNotOwnedByCustomer` |
| 3 | Dealership has no active compatible bay, or no active technician with the required skill, at all | `ServiceNotOfferedAtDealership` |
| 4 | `requestedStart` is not after now (per `TimeProvider`) | `StartInPast` |
| 5 | `requestedStart` is beyond the booking horizon (`BookingHorizonDays`, default 90) | `BeyondBookingHorizon` |
| 6 | `requestedStart` is not on the 15-minute grid of the dealership's **local** wall clock (seconds and sub-seconds must be zero) | `StartNotOnGrid` |
| 7 | The **service** interval `[start, start + duration)` is not inside the opening hours of that local date (no row for the weekday = closed) | `OutsideOpeningHours` |
| 8 | The vehicle has an overlapping confirmed appointment at **any** dealership | `VehicleAlreadyBooked` |
| 9 | No eligible technician / bay for the interval | `NoQualifiedTechnicianAvailable` / `NoCompatibleBayAvailable` |

## Intervals: service vs blocked

- **Service interval** `[start, serviceEnd)`, `serviceEnd = start + serviceType.Duration`. Must fit opening
  hours and the technician's shift. The vehicle is occupied for this interval only.
- **Blocked interval** `[start, blockedUntil)`, `blockedUntil = serviceEnd + serviceType.Buffer` (default 0).
  The technician and bay are occupied for this interval. The buffer **may run past closing time or the
  end of the shift** (clean-up after the last job).
- Both instants are stored on the appointment; later changes to a service type never alter existing appointments.
- A duration that is not a multiple of 15 minutes is allowed; the resource is next bookable at the
  first grid point at or after `blockedUntil`.

## Capacity rules

- **Technician is eligible** when: belongs to the dealership, is active, holds `serviceType.RequiredSkill`,
  has a shift covering the whole **service** interval, and has no confirmed appointment whose blocked
  interval overlaps the requested blocked interval.
- **Bay is eligible** when: belongs to the dealership, is active, its `BayType` satisfies
  `serviceType.RequiredBayType`, and has no overlapping confirmed blocked interval.
  Compatibility: a `Standard` job can use any bay type; `Lift`, `EV` and `Alignment` jobs need a bay of that exact type.
- The job must fit inside **both** opening hours and the shift; a shift that extends beyond opening hours
  does not extend the bookable window.

## Overlap semantics

Half-open intervals: `a.Start < b.End && b.Start < a.End`. Back-to-back (`10:00–11:00`, `11:00–12:00`)
does **not** overlap. Only `Confirmed` appointments count.

## Allocation policy (`SlotAllocator`)

Deterministic, so tests and demos are reproducible. Both policies are **best fit**: never spend a scarce
resource on a job that a less specialised one can do.

1. **Bay: best fit.** Prefer the least specialised compatible bay (a `Standard` job never takes the
   `EV` bay while a `Standard` bay is free). Tie-break by bay ID.
2. **Technician: best fit, then least loaded.** Fewest skills held (protects specialists); then fewest
   booked minutes on that local date; then technician ID.
3. If persisting fails with an exclusion violation on a technician or bay (a concurrent request won
   the race), exclude that resource and retry allocation, at most `MaxAllocationAttempts` (3) in total.
   When attempts run out, return the capacity error for the resource that conflicted last.
   PostgreSQL reports one violated constraint at a time; a second conflict surfaces on the next attempt.
4. A violation on the **vehicle** constraint is not retried: return `VehicleAlreadyBooked`.
5. A capacity error (`NoQualified…`, `NoCompatible…`) includes up to `AlternativesToSuggest` (3) start
   times: the earliest free starts **after** the requested start, within `AlternativesSearchDays` (7)
   local days, never in the past, and not overlapping the vehicle's other bookings. The list may be empty.

## Time and calendars

- Store UTC. Opening hours and shifts are one `[opens, closes)` range per weekday in dealership-local
  time, evaluated by converting the UTC interval with the dealership's IANA zone.
- `closes > opens` on the same date: no `24:00`, no ranges crossing midnight, no split shifts or lunch
  breaks. Appointments therefore never span local midnight.
- Daylight saving: `requestedStart` carries an offset, so it is an exact instant and always exists.
  Durations are elapsed time, never wall-clock difference. On a transition day a local range such as
  `08:00–17:00` may be 8 or 10 elapsed hours; availability steps through **elapsed** 15-minute
  increments mapped to local time, so no slot is duplicated or skipped.
- The response always includes `localStart`, so an advisor who sent the wrong offset sees the booked local time.

## Idempotency

`POST /appointments` accepts an `Idempotency-Key`; keys are global.

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
| `Confirmed`, start in the future | `200`, status `Cancelled`, `cancelledAtUtc` set; the slot is immediately bookable |
| Already `Cancelled` | `200`, unchanged (idempotent) |
| `Confirmed`, start ≤ now | `AppointmentAlreadyStarted` (409) |
| Unknown ID | `NotFound` (404) |

## Availability query

`date` is a dealership-local date. Past dates and dates beyond the booking horizon → `422`. For today,
start times at or before now are excluded. Unknown dealership or service type → `404`.

## Out of scope (documented, not implemented)

Authentication, notifications, payments, rescheduling, multi-technician jobs, multi-day jobs,
technician leave or sickness, public holidays and one-off closures, split shifts and lunch breaks,
`Completed` / no-show statuses, customer self-service portal.
