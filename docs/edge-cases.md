# Edge-case catalogue

Every row must map to at least one test. Keep the **Test** column current (`/tdd` and the
`test-auditor` agent rely on it). Rules behind each row: `.claude/rules/booking-domain.md`.

## End-to-end booking

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-72 | An advisor books an oil change for a customer's vehicle at 10:00 on a Tuesday, with a technician and a bay free | `201` with `Location` and the confirmed appointment (customer, vehicle, service type, dealership, technician and bay IDs, start, end, status `Confirmed`); exactly one row in `appointments` with those same values | Integration | `PostAppointmentTests.PostAppointment_WhenTechnicianAndBayAreFree_ReturnsCreatedAndStoresConfirmedAppointment` |

## Overlap and capacity

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-73 | Another vehicle requests exactly the same time (10:00) as an existing 10:00–11:00 appointment, with only one technician and one bay | `409 NoQualifiedTechnicianAvailable`; only the first appointment is stored | Integration | `PostAppointmentTests.PostAppointment_WhenTechnicianAndBayAreBusyAtTheSameTime_ReturnsNoQualifiedTechnicianAndStoresNothing` |
| EC-01 | Back-to-back bookings (10:00–11:00 then 11:00–12:00) on the same technician/bay | Both succeed (half-open intervals) | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenTechniciansJobsAreBackToBack_ChoosesThatTechnician`, `…_WhenBaysJobsAreBackToBack_ChoosesThatBay`. Integration: `PostAppointmentTests.PostAppointment_WhenRequestStartsAsThePreviousAppointmentEnds_ReturnsCreatedOnTheSameTechnicianAndBay` |
| EC-02 | Requested interval partially overlaps an existing confirmed appointment (technician A busy 09:30–10:30, request 10:00–11:00; same for a bay) | Resource is not eligible: another free one is chosen, or `409` if it was the only one | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenBestFitTechnicianIsBusyForPartOfService_ChoosesAnotherQualifiedTechnician`, `…_WhenOnlyTechnicianHasAnOverlappingJob_ReturnsNoQualifiedTechnicianAvailable`, `…_WhenBestFitBayIsBusyForPartOfService_ChoosesAnotherCompatibleBay`, `…_WhenOnlyBayHasAnOverlappingJob_ReturnsNoCompatibleBayAvailable`. Integration: `PostAppointmentTests.PostAppointment_WhenTechnicianAndBayAreBusyForPartOfTheDuration_ReturnsNoQualifiedTechnicianAndStoresNothing` |
| EC-04 | Service would end after the only qualified technician's shift (shift 08:00–17:00: 16:30 for 60 min rejected, 16:00 for 60 min booked); shift starting after opening (09:00 vs 08:00, request 08:00) | `409 NoQualifiedTechnicianAvailable` / booked at the boundary | Unit | `SlotAllocatorTests.Allocate_WhenServiceDoesNotFitOnlyTechniciansShift_ReturnsNoQualifiedTechnicianAvailable`, `…_WhenServiceStartsAtShiftStartOrEndsAtShiftEnd_ChoosesThatTechnician` |
| EC-05 | Technician qualified but no compatible bay type free (EV job, EV bay busy, Standard bays free) | `409 NoCompatibleBayAvailable` | Unit | `SlotAllocatorTests.Allocate_WhenEvJobAndOnlyEvBayIsBusy_ReturnsNoCompatibleBayAvailable` |
| EC-06 | Bay free but no technician with the required skill free (only EV technician busy, General-only free) | `409 NoQualifiedTechnicianAvailable` | Unit | `SlotAllocatorTests.Allocate_WhenEvJobAndOnlyEvTechnicianIsBusy_ReturnsNoQualifiedTechnicianAvailable` |
| EC-13 | Cancelled appointment in the requested interval | Slot is bookable | Integration | TBD |
| EC-22 | Dealership has no active compatible bay, or no active technician with the skill, at all (none exist, or only inactive ones) | `422 ServiceNotOfferedAtDealership` (not 409); checked before past/grid/hours (rule 3) | Unit | `BookingValidatorTests.Validate_WhenDealershipHasNoBayOfTheRequiredType_ReturnsServiceNotOfferedAtDealership`, `…_WhenNoTechnicianHoldsTheRequiredSkill_…`, `…_WhenOnlyCompatibleBayIsInactive_…`, `…_WhenOnlySkilledTechnicianIsInactive_…`, `…_WhenDealershipHasNoBays_…`, `…_WhenDealershipHasNoTechnicians_…` |
| EC-23 | Technician has no shift on that weekday (only qualified technician, Saturday) | Not eligible → `409 NoQualifiedTechnicianAvailable` | Unit | `SlotAllocatorTests.Allocate_WhenOnlyQualifiedTechnicianHasNoShiftThatWeekday_ReturnsNoQualifiedTechnicianAvailable` |
| EC-62 | Standard job at a dealership with only Lift and EV bays | Service is offered (no 422); a Lift bay is used | Unit | `BookingValidatorTests.Validate_WhenStandardJobAndOnlyLiftAndEvBaysExist_IsAccepted`, `SlotAllocatorTests.Allocate_WhenStandardJobAndOnlyLiftAndEvBaysExist_ChoosesLiftBay` |
| EC-63 | An inactive technician or bay that would otherwise win (fewer skills, lower ID) | Never chosen | Unit | `SlotAllocatorTests.Allocate_WhenBestFitTechnicianIsInactive_ChoosesAnActiveTechnician`, `…_WhenBestFitBayIsInactive_ChoosesAnActiveBay` |
| EC-64 | A technician or bay of another dealership is passed to the `Dealership` aggregate | Rejected at construction (ADR-0002 #21), so it can never be chosen; EC-46 is the database backstop | Unit | TBD |
| EC-65 | Neither a qualified technician nor a compatible bay is free | `409 NoQualifiedTechnicianAvailable` (ADR-0002 #17) | Unit | `SlotAllocatorTests.Allocate_WhenNeitherTechnicianNorBayIsFree_ReturnsNoQualifiedTechnicianAvailable` |
| EC-69 | Skilled, active technician with no shift on any weekday | Service still offered; booking gets `409 NoQualifiedTechnicianAvailable` (ADR-0002 #19) | Unit | `BookingValidatorTests.Validate_WhenSkilledTechnicianHasNoShifts_IsAccepted`, `SlotAllocatorTests.Allocate_WhenOnlySkilledTechnicianHasNoShiftsAtAll_ReturnsNoQualifiedTechnicianAvailable` |
| EC-24 | Shift extends beyond opening hours (07:00–19:00 vs 08:00–18:00), request at 07:30 | `422 OutsideOpeningHours`: the job must fit both | Unit | `BookingValidatorTests.Validate_WhenBookingIsBeforeOpeningButInsideTechniciansShift_ReturnsOutsideOpeningHours` |
| EC-25 | Service duration not a multiple of 15 minutes (50 min at 10:00) | Booked 10:00–10:50; the resource is next bookable at 11:00 | Unit | TBD |
| EC-26 | Buffer runs past closing (60 min + 15 min buffer at 16:00, closes 17:00) | Booked; technician and bay blocked until 17:15 | Unit | TBD |
| EC-27 | Buffer blocks the next job (A 10:00–11:00 + 15 min buffer; B requests 11:00 on the same resources) | Same resources not eligible at 11:00; B can get them at 11:15 | Unit | TBD |
| EC-28 | Buffer does not block the vehicle (same vehicle, next job at 11:00 on other resources) | Booked | Integration | TBD |

## Allocation policy

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-07 | Best-fit bay: Standard job with Standard and EV bays free | Standard bay chosen | Unit | `SlotAllocatorTests.Allocate_WhenStandardAndEvBaysAreFree_ChoosesStandardBay` |
| EC-08 | Best-fit technician: General job, `{General}` and `{General, EV}` technicians both free | `{General}` technician chosen | Unit | `SlotAllocatorTests.Allocate_WhenGeneralistAndEvTechnicianAreFree_ChoosesGeneralist` |
| EC-29 | Technicians with equal skill counts: fewest blocked minutes that date wins; full tie broken by ID | Deterministic choice | Unit | `SlotAllocatorTests.Allocate_WhenSkillCountsAreEqual_ChoosesTechnicianWithFewestBookedMinutesThatDay`, `…_WhenEvAndAlignmentTechniciansAreFree_ChoosesTheLessBookedOne`, `…_WhenSkillsAndWorkloadAreEqual_ChoosesLowerTechnicianId`, `…_WhenTechnicianIsBusierOnAnotherDate_CountsOnlyThatDatesBookingsAsWorkload` |
| EC-66 | Standard job, no Standard bay free; Lift, Alignment and EV bays free | Lift bay chosen (order `Standard < Lift < Alignment < EV`, ADR-0002 #18); EV only when nothing else is free | Unit | `SlotAllocatorTests.Allocate_WhenStandardBayIsBusy_ChoosesLiftBeforeAlignmentAndEv`, `…_WhenStandardAndLiftBaysAreBusy_ChoosesAlignmentBeforeEv`, `…_WhenOnlyEvBayIsFreeForStandardJob_ChoosesEvBay` |
| EC-67 | Two equally specialised bays free | Lower bay ID chosen | Unit | `SlotAllocatorTests.Allocate_WhenTwoEquallySpecialisedBaysAreFree_ChoosesLowerBayId` |
| EC-68 | `{General}` technician has 6 h booked, `{General, EV}` idle | `{General}` chosen: skill count comes before workload | Unit | `SlotAllocatorTests.Allocate_WhenGeneralistHasSixHoursBookedAndEvTechnicianIsIdle_ChoosesGeneralist` |
| EC-71 | Specialised jobs are bookable: an EV job and an Alignment job, with the matching technician and bay free | Passes validation; allocated to those resources (ADR-0002 #9) | Unit | `BookingValidatorTests.Validate_WhenEvJobAtDealershipWithMixedStaffAndBays_IsAccepted`, `…_WhenAlignmentJobAtDealershipWithMixedStaffAndBays_IsAccepted`, `SlotAllocatorTests.Allocate_WhenEvJobAndEvTechnicianAndEvBayAreFree_ChoosesThem`, `…_WhenAlignmentJobAndAlignmentTechnicianAndAlignmentBayAreFree_ChoosesThem` |

## Validation and input

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-03 | Service does not fit inside that day's opening hours (ends after closing, starts before opening, longer than the day, or runs past midnight) | `422 OutsideOpeningHours` | Unit | `BookingValidatorTests.Validate_WhenServiceDoesNotFitOpeningHours_…`, `…_WhenSaturdayServiceIsOutsideSaturdayHours_…`, `…_WhenServiceTouchesOpeningOrClosingTime_IsAccepted`. Past midnight: not covered (see *Deferred*) |
| EC-09 | Requested start is now or earlier | `422 StartInPast` | Unit | `BookingValidatorTests.Validate_WhenStartIsNowOrEarlier_ReturnsStartInPast`. Earlier date: not covered (see *Deferred*) |
| EC-10 | Requested start not on the 15-minute grid (10:10) | `422 StartNotOnGrid` | Unit | `BookingValidatorTests.Validate_WhenStartIsBetweenQuarterHours_…` |
| EC-19 | Unknown customer / vehicle / service type / dealership | `404` | Integration | TBD |
| EC-20 | Vehicle does not belong to the customer | `422 VehicleNotOwnedByCustomer` | Unit | `BookingValidatorTests.Validate_WhenVehicleBelongsToAnotherCustomer_ReturnsVehicleNotOwnedByCustomer` |
| EC-30 | `requestedStart` with an offset (`2026-11-03T10:00:00+07:00`) | Out of scope: the client always sends a plain time (ADR-0002 #25). Not checked; today the server converts it to its own clock | — | None, by decision |
| EC-31 | `requestedStart` with seconds or milliseconds (`10:00:30`) | `422 StartNotOnGrid` | Unit | `BookingValidatorTests.Validate_WhenStartHasSecondsOrMilliseconds_ReturnsStartNotOnGrid` |
| EC-32 | `requestedStart` beyond the 90-day horizon | `422 BeyondBookingHorizon` | Unit | TBD |
| EC-33 | Dealership closed that weekday (no opening-hours row) | `422 OutsideOpeningHours` | Unit | `BookingValidatorTests.Validate_WhenDealershipIsClosedThatWeekday_ReturnsOutsideOpeningHours` |
| EC-34 | Request violates several rules at once (past **and** off-grid) | First error in the documented order (`StartInPast`) | Unit | TBD |
| EC-35 | `Idempotency-Key` empty or longer than 64 characters | `400` | Integration | TBD |
| EC-58 | Same-day walk-in: at 09:00 the advisor books 09:15 (or 14:00) today | Accepted; no minimum notice (ADR-0002 #14) | Unit | `BookingValidatorTests.Validate_WhenBookingFollowsEveryRule_IsAccepted` |
| EC-59 | Quarter-hour starts 10:15, 10:30, 10:45 | Accepted (grid is 15 minutes, not hourly) | Unit | `BookingValidatorTests.Validate_WhenBookingFollowsEveryRule_IsAccepted` |
| EC-60 | Saturday half-day: 12:00 for 60 min accepted; 08:30 rejected (Saturday opens 09:00) | Accepted / `422 OutsideOpeningHours` | Unit | `…_WhenBookingFollowsEveryRule_IsAccepted`, `…_WhenSaturdayServiceIsOutsideSaturdayHours_…` |

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
| EC-17 | **N ≥ 20 concurrent requests for the last free slot** | Exactly one `201`, others `409`, one row | Integration | `BookingRaceTests.PostAppointment_WhenTwentyRequestsRaceForTheLastSlot_ReturnsOneCreatedAndNoQualifiedTechnicianForTheRestAndStoresOneAppointment` |
| EC-74 | Two requests read the busy intervals before either saves; the second save is rejected on the technician constraint | `409 NoQualifiedTechnicianAvailable`, not `500`; one row | Integration | `BookingRaceTests.PostAppointment_WhenRequestReadBusyIntervalsBeforeTheFirstBookingWasSaved_ReturnsNoQualifiedTechnicianAndStoresOneAppointment` |
| EC-76 | Two conflicting inserts wait on each other and PostgreSQL aborts one with `40P01` (deadlock) instead of `23P01` | Prevented: the save locks the technician row, then the bay row, before the insert, so contenders queue and the loser gets `409` as EC-74/75; never `500`. Same vehicle on different technicians and bays can still deadlock (rule 8, deferred) | Integration | `BookingRaceTests` EC-17, run repeatedly (not deterministic) |
| EC-75 | As EC-74, but the conflict is on the bay constraint (two technicians, one bay) | `409 NoCompatibleBayAvailable`; one row | Integration | `BayRaceTests.PostAppointment_WhenRequestReadBusyIntervalsBeforeTheBayWasTaken_ReturnsNoCompatibleBayAndStoresOneAppointment` |
| EC-77 | 20 saves at the same time with different technicians and the same bay | One saved; 19 `NoCompatibleBayAvailable`; no exception (the bay lock prevents the `40P01`) | Integration | `LockOrderRaceTests.AddAsync_WhenTwentySavesOnDifferentTechniciansRaceForTheSameBay_ReturnsOneSavedAndNoCompatibleBayForTheRestAndStoresOneAppointment` |
| EC-78 | 20 saves at the same time with the same technician and different bays | One saved; 19 `NoQualifiedTechnicianAvailable`; no exception (the technician lock prevents the `40P01`) | Integration | `LockOrderRaceTests.AddAsync_WhenTwentySavesOnDifferentBaysRaceForTheSameTechnician_ReturnsOneSavedAndNoQualifiedTechnicianForTheRestAndStoresOneAppointment` |
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
| EC-57 | Opening hours or shift with `closes ≤ opens` or `24:00` | Rejected by a `CHECK` constraint (and by `TimeRange` in the domain) | Unit + Integration | TBD |

## Deferred (known gaps, scheduled)

Findings from code reviews that were consciously postponed, with where they will be handled.

| Gap | Planned in |
|---|---|
| **Tests removed by the developer on 2026-10-31** to focus on the end-to-end flow (error order, technical foundation; restore from commit `05172c2`). No test or mutation now protects: a resource of another dealership (EC-64; the allocator relies on this check), a job past midnight (EC-03), error order when several rules fail (EC-34, and the order part of EC-22), zero-length slots, empty opening ranges (EC-57), null results | After the end-to-end flow; EC-64 is backed by the foreign key (EC-46) in step 4 |
| A missing ID binds to an empty `Guid`; a missing `requestedStart` binds to the default date | Parked with input validation: not in the brief (developer, 2026-11-02) |
| Rule 4 has no test for a start on an earlier date (EC-09): a past check applied only to today would pass every test | Past-start branch |
| The whole-date busy window is correct but untested: reading only the requested slot makes a technician's other jobs that day count as no workload (ADR-0002 #20), and every test still passes (shown by `code-reviewer` on 2026-11-02). The `Confirmed` filter is also untested, because no test has a cancelled appointment | Whole date: "two technicians" branch. `Confirmed`: with cancellation |
| A non-zero `buffer_minutes` is ignored by `Appointment.Book`, and `blocked_until` is stored permanently; no seeded row triggers it | With buffers |
| Busy-interval query has no lower bound on `start_at`, so it examines the dealership's whole history | With availability |
| The startup migration (`Database:ApplyMigrationsOnStartup`, on in `docker-compose.yml`) has no test: the integration fixture migrates the database itself, so deleting the startup call passes every test. Fix: the fixture sets the flag instead of migrating (changes approved tests) | After the core |
| `/health/live` and `/health/ready` (`api-conventions.md`) are not built, and the compose `api` service has no healthcheck | With logging |
| CA1861 fails the build on generated migrations; the first one was hand-edited. Decide on exempting `**/Migrations/*.cs` | Next migration |
| §6.1 says VINs are 17 characters, but no `CHECK` enforces it | After the core |
| Logging (booked, rejected) and the global `IExceptionHandler` are not built yet, while `docs/system-design.md` §11 describes them | After Docker and the README, before the video (developer, 2026-11-02). Scenarios: `docs/work-packages/step-4-logging.md` |
| `Skill` does not validate input (`"ev"` vs `"EV"`); zero duration is now blocked by a `CHECK` | After the core |
| Two requests for the same vehicle on different technicians and bays can still deadlock (`40P01`, a `500`); the vehicle has no lock and its `23P01` is not translated | With rule 8 |
| Retry after a lost race (EC-18, EC-44) is not built. It needs no change to `Allocate`: the conflicting booking is committed when PostgreSQL reports `23P01`, so re-reading the busy intervals already excludes it. Align `docs/system-design.md` §7 when built | Next package |
| When both the technician and the bay conflict, PostgreSQL reports the constraint it checks first (the technician one today, by creation order), so EC-17 and EC-74 rely on that order for ADR-0002 #17. The retry makes it independent of the order | With the retry |
| EF Core logs each rejected insert at `Error` (`Failed executing DbCommand`), although a lost race is an expected `409` (`Information`) | Logging package |
| Workload tests: a technician with two jobs in a day (sum vs largest) | After the core |
| Bay compatibility is left to the swappable policy; filter `CanServe` in the allocator so a new policy cannot break it | After the core |
| `Dealership` equality by `Id` is unused and untested, and `==` is still reference equality | After the core |
| EC-25 (duration not a multiple of 15 minutes) has no test yet | With buffers |
