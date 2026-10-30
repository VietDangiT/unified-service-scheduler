# Edge-case catalogue

Every row must map to at least one test. Keep the **Test** column current (`/tdd` and the
`test-auditor` agent rely on it). Rules behind each row: `.claude/rules/booking-domain.md`.

## Overlap and capacity

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-01 | Back-to-back bookings (10:00–11:00 then 11:00–12:00) on the same technician/bay | Both succeed (half-open intervals) | Unit + Integration | Unit: `TimeSlotTests.Overlaps_WhenSlotsAreBackToBack_ReturnsFalse`, `Overlaps_WhenSlotsAreApart_ReturnsFalse`. Integration: TBD |
| EC-02 | Requested interval partially overlaps an existing confirmed appointment | Resource is not eligible | Unit | `TimeSlotTests.Overlaps_WhenSlotsShareAnyInstant_ReturnsTrue` (overlap rule; eligibility: TBD in step 3) |
| EC-04 | Service would end after the only qualified technician's shift | `409 NoQualifiedTechnicianAvailable` | Unit | TBD |
| EC-05 | Technician qualified but no compatible bay type free | `409 NoCompatibleBayAvailable` | Unit | TBD |
| EC-06 | Bay free but no technician with the required skill free | `409 NoQualifiedTechnicianAvailable` | Unit | TBD |
| EC-13 | Cancelled appointment in the requested interval | Slot is bookable | Integration | TBD |
| EC-22 | Dealership has no compatible bay type, or no technician with the skill, at all | `422 ServiceNotOfferedAtDealership` (not 409) | Unit | TBD |
| EC-23 | Technician has no shift on that weekday | Not eligible | Unit | TBD |
| EC-24 | Shift extends beyond opening hours (07:00–19:00 vs 08:00–18:00), request at 07:30 | `422 OutsideOpeningHours`: the job must fit both | Unit | TBD |
| EC-25 | Service duration not a multiple of 15 minutes (50 min at 10:00) | Booked 10:00–10:50; the resource is next bookable at 11:00 | Unit | TBD |
| EC-26 | Buffer runs past closing (60 min + 15 min buffer at 16:00, closes 17:00) | Booked; technician and bay blocked until 17:15 | Unit | TBD |
| EC-27 | Buffer blocks the next job (A 10:00–11:00 + 15 min buffer; B requests 11:00 on the same resources) | Same resources not eligible at 11:00; B can get them at 11:15 | Unit | TBD |
| EC-28 | Buffer does not block the vehicle (same vehicle, next job at 11:00 on other resources) | Booked | Integration | TBD |

## Allocation policy

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-07 | Best-fit bay: Standard job with Standard and EV bays free | Standard bay chosen | Unit | TBD |
| EC-08 | Best-fit technician: General job, a General-only and an EV-certified technician both free | General-only technician chosen | Unit | TBD |
| EC-29 | Technicians with equal skills: least booked minutes that day wins; full tie broken by ID | Deterministic choice | Unit | TBD |

## Validation and input

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-03 | Service would end after closing time | `422 OutsideOpeningHours` | Unit | TBD |
| EC-09 | Requested start in the past | `422 StartInPast` | Unit | TBD |
| EC-10 | Requested start not on the 15-minute grid (10:10) | `422 StartNotOnGrid` | Unit | TBD |
| EC-19 | Unknown customer / vehicle / service type / dealership | `404` | Integration | TBD |
| EC-20 | Vehicle does not belong to the customer | `422 VehicleNotOwnedByCustomer` | Unit | TBD |
| EC-30 | `requestedStart` without an offset (`2026-11-03T10:00:00`) | `400` | Integration | TBD |
| EC-31 | `requestedStart` with seconds or milliseconds (`10:00:30`) | `422 StartNotOnGrid` | Unit | TBD |
| EC-32 | `requestedStart` beyond the 90-day horizon | `422 BeyondBookingHorizon` | Unit | TBD |
| EC-33 | Dealership closed that weekday (no opening-hours row) | `422 OutsideOpeningHours` | Unit | TBD |
| EC-34 | Request violates several rules at once (past **and** off-grid) | First error in the documented order (`StartInPast`) | Unit | TBD |
| EC-35 | `Idempotency-Key` empty or longer than 64 characters | `400` | Integration | TBD |

## Time zones and daylight saving

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-11 | Request sent with the wrong offset (`10:00+00:00` for London during BST) | Valid instant; booked at 11:00 local; response `localStart` shows 11:00 | Unit | TBD |
| EC-12 | Booking on a DST transition day | End = start + duration in elapsed (UTC) time | Unit | TBD |
| EC-36 | Opening hours or shift on a DST transition day (08:00–17:00 local = 8 or 10 elapsed hours) | Fit is evaluated on the real interval | Unit | TBD |
| EC-37 | Availability on a DST transition day | No duplicated or missing slot | Unit | TBD |

## Alternatives on a capacity error

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-39 | Requested slot full, later slots free the same day | 3 earliest free starts after the requested one | Unit | TBD |
| EC-40 | Rest of the day full | Alternatives come from the following days, up to 7 days ahead | Unit | TBD |
| EC-41 | Nothing free within 7 days | `409` with an empty `alternatives` array | Unit | TBD |
| EC-42 | Same-day request late in the day | No alternative in the past | Unit | TBD |
| EC-43 | Vehicle already booked later that day | No alternative overlaps the vehicle's booking | Unit | TBD |

## Concurrency and persistence

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-14 | Same vehicle already booked at an overlapping time, at the same **or another** dealership | `409 VehicleAlreadyBooked` | Unit + Integration | TBD |
| EC-17 | **N ≥ 20 concurrent requests for the last free slot** | Exactly one `201`, others `409`, one row | Integration | TBD |
| EC-18 | Constraint violation on first candidate, second candidate free | Booking succeeds on retry | Integration | TBD |
| EC-21 | Service type duration changed after booking | Existing appointment end time unchanged | Integration | TBD |
| EC-44 | Allocation attempts exhausted (3 conflicts in a row) | Capacity error for the resource that conflicted last | Unit | TBD |
| EC-45 | Insert committed but the connection drops before the acknowledgement | The retry finds the appointment by its ID and returns `201`, not a false `409` | Integration | TBD |
| EC-46 | Technician or bay from another dealership written to an appointment (bug path) | Rejected by the composite foreign key | Integration | TBD |

## Idempotency

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-15 | Same `Idempotency-Key`, same payload, sent twice | Same appointment returned, one row | Integration | TBD |
| EC-16 | Same `Idempotency-Key`, different payload | `422 IdempotencyKeyReused` | Integration | TBD |
| EC-47 | Same key sent concurrently | One row; both responses carry the same appointment | Integration | TBD |
| EC-48 | Key replayed after the appointment was cancelled | Appointment returned with status `Cancelled` | Integration | TBD |
| EC-49 | Key whose first attempt was rejected with `409` | Evaluated afresh; may now succeed | Integration | TBD |

## Cancellation

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-50 | Cancel a future confirmed appointment | `200`, `Cancelled`; slot immediately bookable | Integration | TBD |
| EC-51 | Cancel twice, or two cancels concurrently | Both `200`; one state change | Integration | TBD |
| EC-52 | Cancel an appointment that has already started | `409 AppointmentAlreadyStarted` | Unit + Integration | TBD |
| EC-53 | Cancel an unknown appointment | `404` | Integration | TBD |

## Availability query

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-54 | Date in the past, or beyond the booking horizon | `422` | Unit | TBD |
| EC-55 | Today | Start times at or before now excluded | Unit | TBD |
| EC-56 | Service longer than the whole opening day | Empty list | Unit | TBD |

## Reference data

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-57 | Opening hours or shift with `closes ≤ opens` or `24:00` | Rejected by a `CHECK` constraint | Integration | TBD |
