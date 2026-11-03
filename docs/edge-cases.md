# Edge-case catalogue

Every row must map to at least one test. Keep the **Test** column current (`/tdd` and the
`test-auditor` agent rely on it). Rules behind each row: `.claude/rules/booking-domain.md`.

## End-to-end booking

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-72 | An advisor books an oil change for a customer's vehicle at 10:00 on a Tuesday, with a technician and a bay free | `201` with `Location` and the confirmed appointment (customer, vehicle, service type, dealership, technician and bay IDs, start, end, status `Confirmed`); exactly one row in `appointments` with those same values | Integration | `BookingRequestTests.PostAppointment_WhenTechnicianAndBayAreFree_ReturnsCreatedAndStoresTheConfirmedAppointment` |

## Overlap and capacity

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-73 | Another vehicle requests exactly the same time (10:00) as an existing 10:00–11:00 appointment, with only one technician and one bay | `409 NoQualifiedTechnicianAvailable`; only the first appointment is stored | Integration | `CapacityTests.PostAppointment_WhenTheOnlyTechnicianAndBayAreBusyForAnyPartOfTheService_ReturnsNoQualifiedTechnicianAndStoresNothing` (10:00 row) |
| EC-01 | Back-to-back bookings (10:00–11:00 then 11:00–12:00) on the same technician/bay | Both succeed (half-open intervals) | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenTechniciansJobsAreBackToBack_ChoosesThatTechnician`, `…_WhenBaysJobsAreBackToBack_ChoosesThatBay`. Integration: `CapacityTests.PostAppointment_WhenServiceIsBackToBackWithTheOtherAppointment_ReturnsCreatedOnTheSameTechnicianAndBay` (09:00 and 11:00) |
| EC-02 | Requested interval partially overlaps an existing confirmed appointment (technician A busy 09:30–10:30, request 10:00–11:00; same for a bay) | Resource is not eligible: another free one is chosen, or `409` if it was the only one | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenBestFitTechnicianIsBusyForPartOfService_ChoosesAnotherQualifiedTechnician`, `…_WhenOnlyTechnicianHasAnOverlappingJob_ReturnsNoQualifiedTechnicianAvailable`, `…_WhenBestFitBayIsBusyForPartOfService_ChoosesAnotherCompatibleBay`, `…_WhenOnlyBayHasAnOverlappingJob_ReturnsNoCompatibleBayAvailable`. Integration: `CapacityTests.PostAppointment_WhenTheOnlyTechnicianAndBayAreBusyForAnyPartOfTheService_ReturnsNoQualifiedTechnicianAndStoresNothing` (09:30 and 10:30 rows), `CapacityTests.PostAppointment_WhenBestFitTechnicianAndBayAreBusyAndAnotherOfEachIsFree_ReturnsCreatedOnTheOtherTechnicianAndBay` |
| EC-04 | Service would end after the only qualified technician's shift (shift 08:00–17:00: 16:30 for 60 min rejected, 16:00 for 60 min booked); shift starting after opening (09:00 vs 08:00, request 08:00) | `409 NoQualifiedTechnicianAvailable` / booked at the boundary | Unit | `SlotAllocatorTests.Allocate_WhenServiceDoesNotFitOnlyTechniciansShift_ReturnsNoQualifiedTechnicianAvailable`, `…_WhenServiceStartsAtShiftStartOrEndsAtShiftEnd_ChoosesThatTechnician` |
| EC-05 | Technician qualified but no compatible bay type free (EV job, EV bay busy, Standard bays free) | `409 NoCompatibleBayAvailable` | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenEvJobAndOnlyEvBayIsBusy_ReturnsNoCompatibleBayAvailable`. Integration: `CapacityTests.PostAppointment_WhenEvJobAndTheOnlyEvBayIsBusy_ReturnsNoCompatibleBayAndDoesNotUseTheStandardBay` |
| EC-06 | Bay free but no technician with the required skill free (only EV technician busy, General-only free) | `409 NoQualifiedTechnicianAvailable` | Unit | `SlotAllocatorTests.Allocate_WhenEvJobAndOnlyEvTechnicianIsBusy_ReturnsNoQualifiedTechnicianAvailable` |
| EC-13 | Cancelled appointment in the requested interval | Slot is bookable | Integration | `CapacityTests.PostAppointment_WhenTheOnlyTechnicianAndBayHoldOnlyACancelledAppointment_ReturnsCreatedOnThem` |
| EC-22 | Dealership has no active compatible bay, or no active technician with the skill, at all (none exist, or only inactive ones) | `422 ServiceNotOfferedAtDealership` (not 409); checked before past/grid/hours (rule 3) | Unit + Integration | `BookingValidatorTests.Validate_WhenDealershipHasNoBayOfTheRequiredType_ReturnsServiceNotOfferedAtDealership`, `…_WhenNoTechnicianHoldsTheRequiredSkill_…`, `…_WhenOnlyCompatibleBayIsInactive_…`, `…_WhenOnlySkilledTechnicianIsInactive_…`, `…_WhenDealershipHasNoBays_…`, `…_WhenDealershipHasNoTechnicians_…`. Integration: `BookingRequestTests.PostAppointment_WhenNoTechnicianHoldsTheRequiredSkill_ReturnsServiceNotOfferedAtDealershipAndStoresNothing`, `…_WhenTheOnlyTechnicianOrTheOnlyBayIsInactive_ReturnsServiceNotOfferedAtDealershipAndStoresNothing` |
| EC-23 | Technician has no shift on that weekday (only qualified technician, Saturday) | Not eligible → `409 NoQualifiedTechnicianAvailable` | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenOnlyQualifiedTechnicianHasNoShiftThatWeekday_ReturnsNoQualifiedTechnicianAvailable`. Integration: `CapacityTests.PostAppointment_WhenTheOnlyTechnicianHasNoShiftThatWeekday_ReturnsNoQualifiedTechnicianAndStoresNothing` |
| EC-62 | Standard job at a dealership with only Lift and EV bays | Service is offered (no 422); a Lift bay is used | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenStandardJobAndOnlyLiftAndEvBaysExist_IsAccepted`, `SlotAllocatorTests.Allocate_WhenStandardJobAndOnlyLiftAndEvBaysExist_ChoosesLiftBay`. Integration: `AllocationTests.PostAppointment_WhenSeveralBayTypesAreFree_ReturnsCreatedOnTheLeastSpecialisedBay` (EV, Alignment, Lift row) |
| EC-63 | An inactive technician or bay that would otherwise win (fewer skills, lower ID) | Never chosen | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenBestFitTechnicianIsInactive_ChoosesAnActiveTechnician`, `…_WhenBestFitBayIsInactive_ChoosesAnActiveBay`. Integration: `CapacityTests.PostAppointment_WhenATechnicianAndABayAreInactive_ReturnsCreatedOnTheActiveOnes` |
| EC-64 | A technician or bay of another dealership is passed to the `Dealership` aggregate | Rejected at construction (ADR-0002 #21), so it can never be chosen; EC-46 is the database backstop | Unit | TBD |
| EC-65 | Neither a qualified technician nor a compatible bay is free | `409 NoQualifiedTechnicianAvailable` (ADR-0002 #17) | Unit | `SlotAllocatorTests.Allocate_WhenNeitherTechnicianNorBayIsFree_ReturnsNoQualifiedTechnicianAvailable` |
| EC-69 | Skilled, active technician with no shift on any weekday | Service still offered; booking gets `409 NoQualifiedTechnicianAvailable` (ADR-0002 #19) | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenSkilledTechnicianHasNoShifts_IsAccepted`, `SlotAllocatorTests.Allocate_WhenOnlySkilledTechnicianHasNoShiftsAtAll_ReturnsNoQualifiedTechnicianAvailable`. Integration: `CapacityTests.PostAppointment_WhenTheOnlyTechnicianHasNoShiftThatWeekday_ReturnsNoQualifiedTechnicianAndStoresNothing` (a shift on Monday only) |
| EC-24 | Shift extends beyond opening hours (07:00–19:00 vs 08:00–18:00), request at 07:30 | `422 OutsideOpeningHours`: the job must fit both | Unit | `BookingValidatorTests.Validate_WhenBookingIsBeforeOpeningButInsideTechniciansShift_ReturnsOutsideOpeningHours` |
| EC-25 | Service duration not a multiple of 15 minutes (50 min at 10:00) | Booked 10:00–10:50; the resource is next bookable at 11:00 | Integration | Integration: `CapacityTests.PostAppointment_WhenServiceTakesFiftyMinutes_ReturnsCreatedEndingAtTenFiftyAndTheResourcesAreBookableAtEleven` |
| EC-26 | Buffer runs past closing (60 min + 15 min buffer at 16:00, closes 17:00) | Booked; technician and bay blocked until 17:15 | Unit | TBD |
| EC-27 | Buffer blocks the next job (A 10:00–11:00 + 15 min buffer; B requests 11:00 on the same resources) | Same resources not eligible at 11:00; B can get them at 11:15 | Unit | TBD |
| EC-28 | Buffer does not block the vehicle (same vehicle, next job at 11:00 on other resources) | Booked | Integration | TBD |

## Allocation policy

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-07 | Best-fit bay: Standard job with Standard and EV bays free | Standard bay chosen | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenStandardAndEvBaysAreFree_ChoosesStandardBay`. Integration: `AllocationTests.PostAppointment_WhenSeveralBayTypesAreFree_ReturnsCreatedOnTheLeastSpecialisedBay` (EV, Lift, Standard row) |
| EC-08 | Best-fit technician: General job, `{General}` and `{General, EV}` technicians both free | `{General}` technician chosen | Unit + Integration | Unit: `SlotAllocatorTests.Allocate_WhenGeneralistAndEvTechnicianAreFree_ChoosesGeneralist`. Integration: `AllocationTests.PostAppointment_WhenAGeneralistAndAnEvTechnicianAreFree_ReturnsCreatedOnTheGeneralist` |
| EC-29 | Technicians with equal skill counts: fewest blocked minutes that date wins; full tie broken by ID | Deterministic choice | Unit + Integration | `SlotAllocatorTests.Allocate_WhenSkillCountsAreEqual_ChoosesTechnicianWithFewestBookedMinutesThatDay`, `…_WhenEvAndAlignmentTechniciansAreFree_ChoosesTheLessBookedOne`, `…_WhenSkillsAndWorkloadAreEqual_ChoosesLowerTechnicianId`, `…_WhenTechnicianIsBusierOnAnotherDate_CountsOnlyThatDatesBookingsAsWorkload`. Integration: `AllocationTests.PostAppointment_WhenTechniciansHaveEqualSkills_ReturnsCreatedOnTheOneWithLessWorkThatDate` |
| EC-66 | Standard job, no Standard bay free; Lift, Alignment and EV bays free | Lift bay chosen (order `Standard < Lift < Alignment < EV`, ADR-0002 #18); EV only when nothing else is free | Unit + Integration | `SlotAllocatorTests.Allocate_WhenStandardBayIsBusy_ChoosesLiftBeforeAlignmentAndEv`, `…_WhenStandardAndLiftBaysAreBusy_ChoosesAlignmentBeforeEv`, `…_WhenOnlyEvBayIsFreeForStandardJob_ChoosesEvBay`. Integration: `AllocationTests.PostAppointment_WhenSeveralBayTypesAreFree_ReturnsCreatedOnTheLeastSpecialisedBay` |
| EC-67 | Two equally specialised bays free | Lower bay ID chosen | Unit | `SlotAllocatorTests.Allocate_WhenTwoEquallySpecialisedBaysAreFree_ChoosesLowerBayId` |
| EC-68 | `{General}` technician has 6 h booked, `{General, EV}` idle | `{General}` chosen: skill count comes before workload | Unit | `SlotAllocatorTests.Allocate_WhenGeneralistHasSixHoursBookedAndEvTechnicianIsIdle_ChoosesGeneralist` |
| EC-71 | Specialised jobs are bookable: an EV job and an Alignment job, with the matching technician and bay free | Passes validation; allocated to those resources (ADR-0002 #9) | Unit | `BookingValidatorTests.Validate_WhenEvJobAtDealershipWithMixedStaffAndBays_IsAccepted`, `…_WhenAlignmentJobAtDealershipWithMixedStaffAndBays_IsAccepted`, `SlotAllocatorTests.Allocate_WhenEvJobAndEvTechnicianAndEvBayAreFree_ChoosesThem`, `…_WhenAlignmentJobAndAlignmentTechnicianAndAlignmentBayAreFree_ChoosesThem` |

## Validation and input

| ID | Edge case | Expected outcome | Suite | Test |
|---|---|---|---|---|
| EC-03 | Service does not fit inside that day's opening hours (ends after closing, starts before opening, longer than the day, or runs past midnight) | `422 OutsideOpeningHours` | Unit + Integration | `BookingValidatorTests.Validate_WhenServiceDoesNotFitOpeningHours_…`, `…_WhenSaturdayServiceIsOutsideSaturdayHours_…`, `…_WhenServiceTouchesOpeningOrClosingTime_IsAccepted`. Integration: `BookingRequestTests.PostAppointment_WhenServiceIsNotInsideOpeningHours_ReturnsOutsideOpeningHoursAndStoresNothing`, `…_WhenServiceTouchesOpeningOrClosingTime_ReturnsCreated`. Past midnight: not covered (see *Deferred*) |
| EC-09 | Requested start is now or earlier | `422 StartInPast` | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenStartIsNowOrEarlier_ReturnsStartInPast`. Integration: `BookingRequestTests.PostAppointment_WhenStartIsNowOrEarlier_ReturnsStartInPastAndStoresNothing` (now, and an earlier date on an open day) |
| EC-10 | Requested start not on the 15-minute grid (10:10) | `422 StartNotOnGrid` | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenStartIsBetweenQuarterHours_…`. Integration: `BookingRequestTests.PostAppointment_WhenStartIsOffTheQuarterHourGrid_ReturnsStartNotOnGridAndStoresNothing` (10:10 row) |
| EC-19 | Unknown customer / vehicle / service type / dealership | `404` | Integration | `BookingRequestTests.PostAppointment_WhenAReferenceDoesNotExist_ReturnsNotFoundAndStoresNothing` |
| EC-20 | Vehicle does not belong to the customer | `422 VehicleNotOwnedByCustomer` | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenVehicleBelongsToAnotherCustomer_ReturnsVehicleNotOwnedByCustomer`. Integration: `BookingRequestTests.PostAppointment_WhenVehicleBelongsToAnotherCustomer_ReturnsVehicleNotOwnedByCustomerAndStoresNothing` |
| EC-30 | `requestedStart` with an offset (`2026-11-03T10:00:00+07:00`) | Out of scope: the client always sends a plain time (ADR-0002 #25). Not checked; today the server converts it to its own clock | — | None, by decision |
| EC-31 | `requestedStart` with seconds or milliseconds (`10:00:30`) | `422 StartNotOnGrid` | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenStartHasSecondsOrMilliseconds_ReturnsStartNotOnGrid`. Integration: `BookingRequestTests.PostAppointment_WhenStartIsOffTheQuarterHourGrid_ReturnsStartNotOnGridAndStoresNothing` (10:00:30 row) |
| EC-32 | `requestedStart` beyond the 90-day horizon | `422 BeyondBookingHorizon` | Unit | TBD |
| EC-33 | Dealership closed that weekday (no opening-hours row) | `422 OutsideOpeningHours` | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenDealershipIsClosedThatWeekday_ReturnsOutsideOpeningHours`. Integration: `BookingRequestTests.PostAppointment_WhenServiceIsNotInsideOpeningHours_ReturnsOutsideOpeningHoursAndStoresNothing` (Wednesday row) |
| EC-34 | Request violates several rules at once (past **and** off-grid) | First error in the documented order (`StartInPast`) | Unit | TBD |
| EC-35 | `Idempotency-Key` empty or longer than 64 characters | `400` | Integration | TBD |
| EC-58 | Same-day walk-in: at 09:00 the advisor books 09:15 (or 14:00) today | Accepted; no minimum notice (ADR-0002 #14) | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenBookingFollowsEveryRule_IsAccepted`. Integration: `BookingRequestTests.PostAppointment_WhenStartIsLaterToday_ReturnsCreated` |
| EC-59 | Quarter-hour starts 10:15, 10:30, 10:45 | Accepted (grid is 15 minutes, not hourly) | Unit + Integration | Unit: `BookingValidatorTests.Validate_WhenBookingFollowsEveryRule_IsAccepted`. Integration: `BookingRequestTests.PostAppointment_WhenStartIsOnAQuarterHour_ReturnsCreated` |
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
| EC-14 | Same vehicle already booked at an overlapping time, at the same **or another** dealership | `409 VehicleAlreadyBooked` | Integration | `CapacityTests.PostAppointment_WhenTheSameVehicleIsAlreadyBookedAtAnOverlappingTime_ReturnsVehicleAlreadyBookedAndStoresNothing` (same dealership, another free pair). Until rule 8 is built, a full dealership or a double-submit on the same pair returns the capacity error instead (ADR-0002 #26). The other-dealership half needs a builder hook for a shared customer |
| EC-17 | **N ≥ 20 concurrent requests for the last free slot** | Exactly one `201`, others `409`, one row | Integration | `ConcurrentBookingTests.PostAppointment_WhenTwentyRequestsRaceForTheOnlyTechnicianAndBay_ReturnsOneCreatedAndNoQualifiedTechnicianForTheRestAndStoresOneAppointment` |
| EC-74 | Two requests read the busy intervals before either saves; the second save is rejected on the technician constraint | `409 NoQualifiedTechnicianAvailable`, not `500`; one row | Integration | `StaleBusyIntervalTests.PostAppointment_WhenRequestReadBusyIntervalsBeforeTheFirstBookingWasSaved_ReturnsNoQualifiedTechnicianAndStoresOneAppointment` |
| EC-76 | Two conflicting inserts wait on each other and PostgreSQL aborts one with `40P01` (deadlock) instead of `23P01` | Prevented: the save locks the technician row, then the bay row, before the insert, so contenders queue and the loser gets `409` as EC-74/75; never `500`. Same vehicle on different technicians and bays can still deadlock (rule 8, deferred) | Integration | `LockOrderRaceTests` EC-77 and EC-78 (each lock caught every run); `ConcurrentBookingTests` EC-17 as a probabilistic second check |
| EC-75 | As EC-74, but the conflict is on the bay constraint (two technicians, one bay) | `409 NoCompatibleBayAvailable`; one row | Integration | `StaleBusyIntervalTests.PostAppointment_WhenRequestReadBusyIntervalsBeforeTheBayWasTaken_ReturnsNoCompatibleBayAndStoresOneAppointment` |
| EC-77 | 20 saves at the same time with different technicians and the same bay | One saved; 19 `NoCompatibleBayAvailable`; no exception (the bay lock prevents the `40P01`) | Integration | `LockOrderRaceTests.AddAsync_WhenTwentySavesOnDifferentTechniciansRaceForTheSameBay_ReturnsOneSavedAndNoCompatibleBayForTheRestAndStoresOneAppointment` |
| EC-78 | 20 saves at the same time with the same technician and different bays | One saved; 19 `NoQualifiedTechnicianAvailable`; no exception (the technician lock prevents the `40P01`) | Integration | `LockOrderRaceTests.AddAsync_WhenTwentySavesOnDifferentBaysRaceForTheSameTechnician_ReturnsOneSavedAndNoQualifiedTechnicianForTheRestAndStoresOneAppointment` |
| EC-79 | A successful booking is logged | One `AppointmentBooked` entry at `Information` with the appointment, dealership, technician and bay IDs; no personal data in any entry | Integration | `BookingLogTests.PostAppointment_WhenBookingSucceeds_LogsOneAppointmentBookedWithTheResponseIdsAndNoPersonalData` |
| EC-80 | A rejected booking (closed day) is logged | One `BookingRejected` entry at `Information` with `Reason` = the error code; nothing at `Error`; no row | Integration | `BookingLogTests.PostAppointment_WhenDealershipIsClosedThatDay_LogsOneBookingRejectedWithOutsideOpeningHoursAndNothingAtError` |
| EC-81 | An unexpected exception while booking | Generic `500` ProblemDetails with `traceId` and no exception details; logged exactly once at `Error` | Integration | `BookingLogTests.PostAppointment_WhenReaderThrowsUnexpectedly_ReturnsGenericProblemAndLogsTheExceptionOnceAtError` |
| EC-82 | The body is not valid JSON | `400`, not a `500` from the global handler; nothing at `Error` | Integration | `BookingLogTests.PostAppointment_WhenBodyIsNotValidJson_ReturnsBadRequestAndLogsNothingAtError` |
| EC-83 | Two technicians and two bays; the first pair is booked at 10:00; another vehicle books 10:30 with the real busy-interval query | `201` on the other technician and bay; two rows (the application check, not the constraint, chooses) | Integration | `CapacityTests.PostAppointment_WhenBestFitTechnicianAndBayAreBusyAndAnotherOfEachIsFree_ReturnsCreatedOnTheOtherTechnicianAndBay` |
| EC-84 | Each booking error has its documented kind | Rule errors `422`, capacity errors and `VehicleAlreadyBooked` `409`, `NotFound` `404` (ADR-0002 #10; `api-conventions.md` status table) | Unit | `BookingErrorsTests.Kind_OfEachBookingError_IsItsDocumentedKind` |
| EC-85 | 3 technicians and 3 bays; 20 parallel requests for the same 10:00 slot | Exactly 3 `201` on distinct technicians and bays; 17 `409`; 3 rows; no `500` (the retry, step 9) | Integration | `ConcurrentBookingTests.PostAppointment_WhenTwentyRequestsRaceForThreeTechniciansAndThreeBays_ReturnsThreeCreatedOnDistinctTechniciansAndBaysAndConflictForTheRest` |
| EC-86 | 2 technicians, 1 bay; the bay is booked at 10:00; another vehicle at 10:00 | `409 NoCompatibleBayAvailable`; 1 row. The application check and the bay constraint give the same answer, so the bay side of the busy-interval query is proven by EC-83 | Integration | `CapacityTests.PostAppointment_WhenATechnicianIsFreeButTheOnlyBayIsBusy_ReturnsNoCompatibleBayAndStoresNothing` |
| EC-87 | EV job; one General and one General+EV technician | The EV technician is chosen (exact skill) | Integration | `CapacityTests.PostAppointment_WhenEvJobAndOnlyOneTechnicianHoldsTheEvSkill_ReturnsCreatedOnTheEvTechnician` |
| EC-88 | A technician shift ends before the service would | Another technician whose shift covers the whole service is chosen; touching shift edges are accepted | Integration | `CapacityTests.PostAppointment_WhenATechnicianShiftDoesNotCoverTheWholeService_ReturnsCreatedOnTheTechnicianWhoseShiftDoes` |
| EC-89 | 2 technicians, 1 bay; 20 parallel requests | Exactly one `201`; every loser `409 NoCompatibleBayAvailable` (the bay is what ran out); 1 row; no `500` | Integration | `ConcurrentBookingTests.PostAppointment_WhenTwentyRequestsRaceForTwoTechniciansAndTheOnlyBay_ReturnsOneCreatedAndConflictForTheRestAndStoresOneAppointment` |
| EC-90 | The save fails with `VehicleAlreadyBooked` | Returned at once, never retried (policy 4) | Unit | `BookAppointmentHandlerTests.HandleAsync_WhenTheSaveFindsTheVehicleAlreadyBooked_ReturnsVehicleAlreadyBookedWithoutTryingAgain` |
| EC-91 | The save fails on the bay; another bay is free | Saved on the other bay | Unit | `BookAppointmentHandlerTests.HandleAsync_WhenTheSaveLosesTheRaceOnTheBay_BooksTheNextCompatibleBay` |
| EC-18 | Constraint violation on first candidate, second candidate free | Booking succeeds on retry | Unit | `BookAppointmentHandlerTests.HandleAsync_WhenTheSaveLosesTheRaceOnTheTechnician_BooksTheNextQualifiedTechnicianWithTheSameAppointment` (unit, fake repository) |
| EC-21 | Service type duration changed after booking | Existing appointment end time unchanged | Integration | TBD |
| EC-44 | Allocation attempts exhausted (3 conflicts in a row) | Capacity error for the resource that conflicted last | Unit | `BookAppointmentHandlerTests.HandleAsync_WhenEverySaveLosesTheRace_StopsAfterThreeAttemptsWithTheCapacityErrorOfTheLastConflict` |
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
| **Tests removed by the developer on 2026-10-31** to focus on the end-to-end flow (error order, technical foundation; restore from commit `05172c2`). No test or mutation now protects: a resource of another dealership (EC-64; the allocator relies on this check), a job past midnight (EC-03), error order when several rules fail (EC-34, and the order part of EC-22), zero-length slots, empty opening ranges (EC-57), null results | After the end-to-end flow; EC-64 relies on the composite foreign key, which no test exercises yet (EC-46 is TBD) |
| A missing ID binds to an empty `Guid`; a missing `requestedStart` binds to the default date | Parked with input validation: not in the brief (developer, 2026-11-02) |
| The vehicle constraint's `WHERE (status = 'Confirmed')` is untested: EC-13 stores a cancelled appointment for another vehicle, so it covers the reader and the technician and bay constraints only. The whole-date workload window is now covered by EC-29 | With cancellation |
| A non-zero `buffer_minutes` is ignored by `Appointment.Book`, and `blocked_until` is stored permanently; no seeded row triggers it | With buffers |
| Busy-interval query has no lower bound on `start_at`, so it examines the dealership's whole history | With availability |
| The startup migration (`Database:ApplyMigrationsOnStartup`, on in `docker-compose.yml`) has no test: the integration fixture migrates the database itself, so deleting the startup call passes every test. Fix: the fixture sets the flag instead of migrating (changes approved tests) | After the core |
| `/health/live` and `/health/ready` (`api-conventions.md`) are not built, and the compose `api` service has no healthcheck | After the core |
| Framework-produced errors other than a bad body (415, 405, a too-large body that Kestrel rejects) are not pinned by a test; a too-large body may still reach the global handler as a `500` | With input validation (EC-30 branch) |
| CA1861 fails the build on generated migrations; the first one was hand-edited. Decide on exempting `**/Migrations/*.cs` | Next migration |
| §6.1 says VINs are 17 characters, but no `CHECK` enforces it | After the core |
| `Skill` does not validate input (`"ev"` vs `"EV"`); zero duration is now blocked by a `CHECK` | After the core |
| **Found by `test-auditor` on 2026-11-03.** (1) and (2) closed by EC-83 and EC-84 (step 7). (3) The test clock's local zone is UTC, so reading `GetUtcNow()` instead of `GetLocalNow()` passes every test while a `TZ` override would shift "now" (invariant 3) | Next package |
| **Deferred by the developer on 2026-11-03 (`code-reviewer`, step 9 major).** Since the retry, a broken busy-interval query is hidden: the constraint plus a retry reach the free pair, so EC-83 and EC-86 pass. It is still caught by EC-29 (workload), and the bay side only by EC-85, because its capacity (3) equals `MaxAllocationAttempts`. Fix: an integration test that calls `GetBusyIntervalsAsync` directly (technician and bay intervals, cancelled rows excluded). EC-83's and EC-86's rows overstate what they prove until then | After submission |
| A retry is not logged at `Warning` and `scheduler.booking.allocation_retries` is not counted; each lost race also adds an EF Core `Error` line | Logging package |
| Two **simultaneous** requests for the same vehicle on different technicians and bays can still deadlock (`40P01`, a `500`), because the vehicle has no row lock. A sequential double booking of a vehicle is now `409 VehicleAlreadyBooked` (ADR-0002 #26, EC-14) | With rule 8 |
| When both the technician and the bay conflict, PostgreSQL reports the constraint it checks first (the technician one today, by creation order), so EC-74 relies on that order for ADR-0002 #17. With the real reader the retry makes the order irrelevant; EC-74 uses a stale reader that never sees the winner, so it still depends on it | Accepted (test-only) |
| EF Core logs each rejected insert at `Error` (`Failed executing DbCommand`), although a lost race is an expected `409` (`Information`) | Logging package |
| Workload tests: a technician with two jobs in a day (sum vs largest) | After the core |
| Bay compatibility is left to the swappable policy; filter `CanServe` in the allocator so a new policy cannot break it | After the core |
| `Dealership` equality by `Id` is unused and untested, and `==` is still reference equality | After the core |
