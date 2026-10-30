# ADR-0002: Scope and business assumptions

- **Status:** Accepted, provisionally (all 13 rows confirmed by the developer on 2026-10-30). To be
  revisited once the test suite and demo run against them; any change follows the process under *Consequences*
- **Date:** 2026-10-30

## Context

The brief is deliberately ambiguous. These assumptions turn it into a buildable, testable specification.

## Decisions

| # | Question | Decision | Rationale |
|---|---|---|---|
| 1 | Who books? | A service advisor via an internal tool; no end-customer authentication | Matches "replace manual booking systems"; keeps auth out of scope |
| 2 | Who chooses technician and bay? | The system allocates automatically. **Best fit for both**: least specialised compatible bay; technician with the fewest skills, then least loaded that day, then ID | Customers do not pick technicians. Best fit protects scarce specialists (EV bay, EV-certified technician) for the jobs only they can do |
| 3 | Requested time unavailable | `409` with a machine-readable reason and the earliest 3 free start times after the requested one, searched up to **7 days** ahead | Actionable for the advisor without a second round trip; still a bounded query |
| 4 | Buffer between jobs | Zero by default, configurable per service type. The **service** must fit opening hours and the shift; the **buffer** may run past closing and blocks only the technician and bay | Real workshops need clean-up time; requiring the buffer before closing would waste the last slot of every day |
| 5 | Technician availability | One weekly shift range per weekday; no leave, sickness, split shifts or lunch breaks | Bounded scope; leave and breaks are natural extensions as unavailability intervals |
| 6 | Client layer | OpenAPI + `.http` file; thin React demo page only if time allows | Brief requires one fully implemented layer (backend) |
| 7 | Slot granularity | 15 minutes on the dealership's local wall clock | Common workshop practice; keeps availability lists small |
| 8 | Multi-day / multi-technician jobs | Out of scope | Different allocation problem; documented as future work |
| 9 | Bay compatibility | A `Standard` job can use any bay type; `Lift`, `EV` and `Alignment` jobs need a bay of that type | Mirrors real workshops; gives the best-fit policy something meaningful to decide |
| 10 | Service impossible at a dealership (no compatible bay or no skilled technician at all) | `422 ServiceNotOfferedAtDealership`, distinct from `409` capacity errors | "Never possible here" and "full right now" need different actions from the advisor |
| 11 | Booking horizon | Up to 90 days ahead (configurable) | Bounds availability queries and the alternatives search; typical for workshop planning |
| 12 | Opening calendar | One opening range per weekday; no public holidays or one-off closures | Bounded scope; closures are a future unavailability calendar |
| 13 | Appointment lifecycle | `Confirmed → Cancelled` only; cancel is idempotent and refused once the appointment has started | `Completed` / no-show need a workshop process this assessment does not model (YAGNI) |

## Consequences

Each decision is reflected in `.claude/rules/booking-domain.md` and in `docs/edge-cases.md`.
Changing one requires updating both and this ADR.
