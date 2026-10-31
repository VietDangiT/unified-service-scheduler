# ADR-0002: Scope and business assumptions

- **Status:** Accepted, provisionally (rows 1–13 confirmed by the developer on 2026-10-30; rows 14–21
  decided by the developer on 2026-10-30 from questions raised by the `domain-analyst` agent;
  rows 22–25 and *Delivery order* added on 2026-10-31). To be
  revisited once the test suite and demo run against them; any change follows the process under *Consequences*
- **Date:** 2026-10-30

## Context

The brief is deliberately ambiguous. These assumptions turn it into a buildable, testable specification.

## Decisions

| # | Question | Decision | Rationale |
|---|---|---|---|
| 1 | Who books? | A service advisor via an internal tool; no end-customer authentication. Vehicle-owner self-booking is a future channel over the same booking core | Matches "replace manual booking systems"; keeps auth out of scope |
| 2 | Who chooses technician and bay? | The system allocates automatically. **Best fit for both**: least specialised compatible bay; technician with the fewest skills, then least loaded that day, then ID | Customers do not pick technicians. Best fit protects scarce specialists (EV bay, EV-certified technician) for the jobs only they can do |
| 3 | Requested time unavailable | `409` with a machine-readable reason and the earliest 3 free start times after the requested one, searched up to **7 days** ahead | Actionable for the advisor without a second round trip; still a bounded query |
| 4 | Buffer between jobs | Zero by default, configurable per service type. The **service** must fit opening hours and the shift; the **buffer** may run past closing and blocks only the technician and bay | Real workshops need clean-up time; requiring the buffer before closing would waste the last slot of every day |
| 5 | Technician availability | One weekly shift range per weekday; no leave, sickness, split shifts or lunch breaks | Bounded scope; leave and breaks are natural extensions as unavailability intervals |
| 6 | Client layer | OpenAPI + `.http` file; thin React demo page only if time allows | Brief requires one fully implemented layer (backend) |
| 7 | Slot granularity | 15 minutes on the clock | Common workshop practice; keeps availability lists small |
| 8 | Multi-day / multi-technician jobs | Out of scope | Different allocation problem; documented as future work |
| 9 | Bay compatibility | A `Standard` job can use any bay type; `Lift`, `EV` and `Alignment` jobs need a bay of that type | Mirrors real workshops; gives the best-fit policy something meaningful to decide |
| 10 | Service impossible at a dealership (no compatible bay or no skilled technician at all) | `422 ServiceNotOfferedAtDealership`, distinct from `409` capacity errors | "Never possible here" and "full right now" need different actions from the advisor |
| 11 | Booking horizon | Up to 90 days ahead (configurable) | Bounds availability queries and the alternatives search; typical for workshop planning |
| 12 | Opening calendar | One opening range per weekday; no public holidays or one-off closures | Bounded scope; closures are a future unavailability calendar |
| 13 | Appointment lifecycle | `Confirmed → Cancelled` only; cancel is idempotent and refused once the appointment has started | `Completed` / no-show need a workshop process this assessment does not model (YAGNI) |
| 14 | Minimum notice for same-day bookings | None: any start after now is accepted (09:15 can be booked at 09:14) | Keeps walk-in and same-day revenue; a configurable lead time is a small later change to rule 4 |
| 15 | "Closed that day" vs "runs past closing" | One error, `OutsideOpeningHours`, for both | Simpler contract for v1; adding a detail field later is backwards-compatible |
| 16 | Company, lease and fleet vehicles (driver is not the registered owner) | Out of scope: booking requires the customer to own the vehicle (`VehicleNotOwnedByCustomer`) | Fleet work is real and valuable, but needs an "authorised driver" model from the DMS; documented as future work |
| 17 | Neither a qualified technician nor a compatible bay is free | `NoQualifiedTechnicianAvailable` (technician error takes precedence) | Technicians are usually the binding constraint; keeps the `rejected{reason}` staffing signal meaningful; no new error code |
| 18 | Which specialised bay a `Standard` job uses when no `Standard` bay is free | Fixed specialisation order `Standard < Lift < Alignment < EV`; take the least specialised free bay, then lowest ID | Deterministic and explainable; keeps the scarcest, most specialised bay (EV) for the jobs only it can do |
| 19 | Skilled, active technician with no shift on any weekday | Still counts as "offered" for rule 3 (active + skill only); bookings get `409 NoQualifiedTechnicianAvailable` | Keeps rule 3 a static capability check; a technician with no shifts is a reference-data problem for the DMS sync to flag |
| 20 | What "least loaded" measures | Blocked minutes (service + buffer) of `Confirmed` appointments on that date | The technician is occupied during clean-up; identical to service minutes while buffers are 0 |
| 21 | A dealership's technician or bay list contains a resource of another dealership (bad reference data) | Rejected when the `Dealership` aggregate is built (programming/data error, fails loudly); rule 3 and the allocator need no filter | The impossible state cannot exist, so "not offered" (422) and "fully booked" (409) can never disagree; the composite foreign key (EC-46) is the database backstop |
| 22 | Managing resources (bays, technicians, opening hours) and technician job views | Out of scope: seeded data is used instead | The brief is about booking against existing capacity; admin screens add no booking behaviour |
| 23 | Are customers, vehicles and service types per dealership or shared? | Shared across dealerships | One customer record wherever they bring the car, and a vehicle cannot be booked at two dealerships at once (rule 8). The cost: a service type has the same duration everywhere |
| 24 | Must the database guarantee the vehicle belongs to the appointment's customer? | No: rule 2 checks it in code when booking | The appointment records who booked at that time. A composite foreign key would block changing a vehicle's owner once it has appointments |
| 25 | Time zones and UTC (decided by the developer on 2026-10-31) | Not used. One clock: `10:00` means 10:00 in the request, the response and the database (`timestamp`, `tsrange`). A request with an offset is rejected with `400` | The brief never mentions time zones; they were an earlier AI-session assumption and solve no booking problem. The cost: every dealership is treated as being on the same clock, and "now" relies on the server clock being set correctly |

## Delivery order

Core first, then the rest (decided by the developer on 2026-10-31):

1. Allocation in the domain (step 3).
2. Database and `POST /api/appointments` (step 4): the straight path first, an advisor books and the appointment
   is saved, end to end. Rejections and other branches follow once that path works. The exclusion constraints
   are in the first migration, so data is protected from the start; until step 5 a request that loses a race gets a `500`.
3. Translating constraint violations, retrying with the next candidate, and the concurrency test (step 5).
4. Alternatives in the `409`, so the advisor can offer another time on the phone (user story US2).
5. Docker, README and the video.

**Parked until the core is done**, in this order: availability query, cancellation, idempotency, booking horizon, buffers, business-event logging, metrics and
dashboards, React page. Their rules and routes stay in `.claude/rules/` as the target behaviour, so a review does not report them as missing;
until built, a capacity error carries only its reason.

## Consequences

Each decision is reflected in `.claude/rules/booking-domain.md` and in `docs/edge-cases.md`.
Changing one requires updating both and this ADR.
