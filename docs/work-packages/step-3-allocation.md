# Work package: step 3, resource allocation (pure domain)

- **Branch:** `feature/allocation` (from `dev`)
- **Status:** approved by the developer on 2026-10-30 (implementer runs as a `general-purpose` substitute, approved)
- **Rules:** `.claude/rules/booking-domain.md` (validation rule 3 and 9, Capacity rules, Allocation policy 1–2);
  ADR-0002 #2, #9, #10, #17–20
- **Scenarios from:** `domain-analyst` run of 2026-10-30, open questions decided by the developer (ADR-0002 #17–20)

## Scope

Choose a technician and a bay for a validated service interval, or return a typed capacity error, and
reject services a dealership can never perform. Pure, synchronous domain code; the caller supplies the
dealership's resources and the busy intervals of confirmed appointments.

**Out of scope:** persistence and exclusion constraints, the `23P01` retry loop, vehicle overlap (rule 8),
alternatives (ADR-0002, *Delivery order* item 4), booking horizon (rule 5), buffers (assume 0; workload already uses the blocked
interval so buffers slot in later), cancellation, idempotency, DST transition days.

## Shape (follow `docs/system-design.md` §5.1)

| Type | Purpose |
|---|---|
| `Skill` (value object), `BayType` (`Standard`, `Lift`, `Alignment`, `EV`, in specialisation order) | Capabilities |
| `ServiceType` (id, duration, required skill, required bay type) | Replaces the bare duration in `BookingRequest` |
| `Technician` (id, dealership id, skills, weekly shifts, active), `ServiceBay` (id, dealership id, type, active) | Resources, owned by `Dealership` |
| `Dealership`: adds technicians and bays, compares by `Id`, `IsOpenFor(slot)` | Deferred items from step 2 |
| `BookingValidator`: rule 3 between rules 2 and 4 | `ServiceNotOfferedAtDealership` |
| `SlotAllocator` with `IBayAllocationPolicy` / `ITechnicianAllocationPolicy` (`BestFitBayPolicy`, `BestFitTechnicianPolicy`) | Returns `Result<Allocation>` |
| Busy intervals per technician and per bay (confirmed appointments, blocked interval) | Input to the allocator |

## Scenarios (tests to write in phase `red`)

| EC | Scenario | Expected (source) |
|---|---|---|
| EC-22 | No compatible bay / no skilled technician at all, or only inactive ones | `ServiceNotOfferedAtDealership` (rule 3) |
| EC-34 ext | Service not offered **and** start in the past / dealership closed | `ServiceNotOfferedAtDealership` (rule order 3 < 4 < 7) |
| EC-62 | Standard job, only Lift and EV bays exist | Offered; Lift bay used (ADR #9, #18) |
| EC-69 | Skilled, active technician with no shifts at all | Offered; `NoQualifiedTechnicianAvailable` (ADR #19) |
| EC-01 | Technician/bay busy 09:00–10:00, request 10:00 | Eligible (half-open) |
| EC-02 | Technician/bay busy 09:30–10:30, request 10:00–11:00 | Not eligible: other one chosen, or 409 if only one |
| EC-04 | Shift 08:00–17:00: 16:30+60 min rejected, 16:00+60 min booked; shift 09:00 start, request 08:00 | `NoQualifiedTechnicianAvailable` / booked |
| EC-23 | Only qualified technician has no Saturday shift | `NoQualifiedTechnicianAvailable` |
| EC-24 | Shift 07:00–19:00, opening 08:00–18:00, request 07:30 | `OutsideOpeningHours` (rule 7 first) |
| EC-05 | EV job, EV bay busy, Standard bays free | `NoCompatibleBayAvailable` |
| EC-06 | EV job, bay free, EV technician busy, General-only free | `NoQualifiedTechnicianAvailable` |
| EC-65 | No technician and no bay free | `NoQualifiedTechnicianAvailable` (ADR #17) |
| EC-63 | Inactive technician/bay that would otherwise win | Never chosen |
| EC-64 | Technician/bay of another dealership passed to `Dealership` | Rejected at construction (ADR #21) |
| EC-07 | Standard job, Standard and EV bays free | Standard bay |
| EC-66 | Standard job, no Standard free; Lift, Alignment, EV free | Lift; EV only as last resort (ADR #18) |
| EC-67 | Two equally specialised bays free | Lower bay ID |
| EC-08 | General job, `{General}` and `{General, EV}` free | `{General}` |
| EC-68 | `{General}` has 6 h booked, `{General, EV}` idle | `{General}` (skills before workload) |
| EC-29 | Equal skill counts | Fewest blocked minutes that local date, then lower ID |
| EC-70 | Auckland technician, bookings on same UTC date but other local date | Not counted as that day's workload |

Assertions name the chosen technician and bay IDs, or the exact `DomainError`.

## Builders (authorised changes)

- New: `TechnicianBuilder` (skills, shifts defaulting to opening hours, active, dealership, readable IDs),
  `ServiceBayBuilder` (type, active, dealership), `ServiceTypeBuilder` (presets: oil change = 60 min,
  General, Standard; EV check = EV, EV; alignment = Alignment, Alignment), a busy-interval helper.
- `DealershipBuilder` default **gains** one active `{General}` technician (shifts = opening hours) and one
  `Standard` bay, so every existing `BookingValidatorTests` case stays valid under rule 3.
- `BookingRequestBuilder` gains `WithServiceType(...)`; `WithDuration(minutes)` stays (General/Standard type).
- Existing approved tests are not edited.

## Mutations the tests must catch (added in phase `green`)

1. Technician busy intervals ignored. 2. Bay busy intervals ignored. 3. Shift check tests only the start.
4. `Standard` job requires an exact `Standard` bay. 5. Specialised job accepts any bay type.
6. Bay order `EV` before `Lift`. 7. Workload compared before skill count. 8. Inactive filter removed.
9. `Dealership` accepts a resource of another dealership. 10. Bay error returned when both are missing. 11. Rule 3 after `StartInPast`.
12. Rule 3 counts inactive resources. 13. Workload uses the UTC date. 14. ID tie-break reversed.

## Approved extras

None: no NuGet packages, no API contract change.

## Revision 1 (red), from the `domain-analyst` check of the red tests

Developer decisions on 2026-10-30: keep the Strategy design (`IBayAllocationPolicy` /
`ITechnicianAllocationPolicy`) as in `docs/system-design.md`; foreign resources are rejected by the
`Dealership` aggregate (ADR-0002 #21). Changes to the red tests (not yet approved, so still editable):

1. EC-64: delete the two allocator tests for another dealership's technician/bay; add two `Dealership`
   construction tests (foreign technician, foreign bay → `ArgumentException`) in
   `tests/ServiceScheduler.UnitTests/Domain/Dealerships/DealershipTests.cs`.
2. EC-29 `…DifferentSkillsOfEqualCount…`: book 60 min on the `{General, Alignment}` technician (ID 1) and
   expect the idle `{General, EV}` technician (ID 2), so only "equal count, then workload" passes.
3. EC-68: the generalist's 6 h as one block 12:00–18:00 (not touching the 10:00–11:00 request).
4. Add the order test: vehicle not owned **and** service not offered → `VehicleNotOwnedByCustomer` (rule 2 before 3).
5. Renames in business language: `…JobsAreBackToBack…` (technician and bay), `…HasAnOverlappingJob…`
   (technician and bay), `…StartsAtShiftStartOrEndsAtShiftEnd…`,
   `…WhenEvAndAlignmentTechniciansAreFree_ChoosesTheLessBookedOne`,
   `…CountsOnlyThatLocalWorkingDaysBookingsAsWorkload`, `…WhenBookingIsBeforeOpeningButInsideTechniciansShift…`.

## Revision 2, from the `code-reviewer` run of 2026-10-31

Major finding: every test with a specialised job expects a rejection, so a bug refusing all such jobs passes.

- **New tests, EC-71:** an EV job and an Alignment job, with the matching technician and bay free, pass
  `BookingValidator.Validate` and are allocated to those resources. Production code is already correct, so
  these are expected to pass immediately ("already covered").
- **Validator setup (from the `domain-analyst` check):** the dealership is mixed, with a generalist (ID 1) and
  the specialist (ID 2), a `Standard` bay (ID 1) and the matching bay (ID 2), as a normal dealership would be.
- **New mutations:** a bay serves only `Standard` jobs; rule 3 rejects every job that is not `Standard`;
  rule 3 requires **all** technicians and bays to match instead of **any**.

The other review findings are in `docs/edge-cases.md`, *Deferred*.
