# Work package: step 7, close the pre-submission audit gaps, then group the small Domain types

- **Branch:** `feature/allocation`, adding to PR #2
- **Status:** the developer chose to close gaps 1 and 2 and then group files on 2026-11-03. Gap 3 (test clock in UTC) stays in *Deferred*.
- **Source:** `test-auditor` on 2026-11-03 (`docs/edge-cases.md`, *Deferred*); `booking-domain.md` *Capacity rules* and *Allocation
  policy*; ADR-0002 #10

## Part A: tests

| EC | Scenario | Expected (source) |
|---|---|---|
| EC-83 | The dealership has a second `General` technician and a second `Standard` bay, both with IDs after the seeded ones. The seeded vehicle books Tuesday 10:00 through the API and gets the seeded technician and bay. A second vehicle books 10:30 through the API with the **real** reader | `201`; the response names the added technician and the added bay; 2 rows in `appointments`, both `Confirmed` (EC-02: "another free one is chosen"; *Allocation policy* 1–2) |
| EC-84 | Each booking error has its documented kind | `Validation` (`422`) for `VehicleNotOwnedByCustomer`, `ServiceNotOfferedAtDealership`, `StartInPast`, `StartNotOnGrid`, `OutsideOpeningHours`; `Conflict` (`409`) for `NoQualifiedTechnicianAvailable`, `NoCompatibleBayAvailable`; `NotFound` (`404`) for `NotFound` (ADR-0002 #10; `api-conventions.md` status table). Unit test, a `[Theory]` on `error.Kind` |

Both pass against today's code ("already covered"). They qualify when these mutations fail against them:

1. `SchedulingReader.GetBusyIntervalsAsync` returns no busy intervals. EC-83 must fail.
2. The reader builds technician busy intervals from `ServiceBayId`. EC-83 must fail.
3. `ServiceNotOfferedAtDealership` becomes `ErrorKind.Conflict`. EC-84 must fail. Add this one to `tools/mutation/mutate.ps1`.

Mutations 1 and 2 are integration-only: apply them by hand to a copy and record the results here.

**EC-83 setup:** a class of its own with its own fixture, as `BayRaceTests` has, so the added technician and bay never change what the
other classes allocate. Add the technician (General skill, Tuesday 08:00–17:00 shift) and the bay (active, `Standard`) idempotently,
with fixed IDs in a new range.

## Part B: group the one-line Domain types (behaviour-neutral)

This is a pure move. Namespaces, type names and members stay the same, so no test changes and the fingerprint is unchanged.

| Into | Types |
|---|---|
| `Customers/Vehicle.cs` | `Vehicle`, `VehicleId`, `CustomerId` |
| `Dealerships/DealershipIds.cs` | `DealershipId`, `ServiceBayId`, `TechnicianId` |
| `Scheduling/Appointment.cs` | `AppointmentId` (next to `Appointment`) |
| `ServiceTypes/ServiceType.cs` | `ServiceTypeId` |
| `Scheduling/SlotAllocator.cs` | `Allocation`, `AllocationRequest` (its input and output) |

Keep these as they are: `Skill`, `DomainError`, and the two policy interfaces (the developer kept the Strategy pattern).

Check that no analyzer forbids several types in one file. If one does, stop and report rather than suppress it.

## Approved extras

No NuGet package, no API contract change.

## Part A results (2026-11-03)

Both tests passed at once ("already covered"), read from the full output: EC-83 `Passed [735 ms]`, EC-84 8 of 8 rows passed.
`domain-analyst`: ready. Re-run by the orchestrator: build clean, 81/81 unit, 14/14 integration, format clean, `mutate.ps1` 27/27.

| Mutation | Result |
|---|---|
| 1. Reader returns no busy intervals (copy, integration) | Killed by EC-83 alone, `Expected: Created / Actual: Conflict`. No other test catches it, which is the audit's gap |
| 2. Technician busy intervals built from `ServiceBayId` (copy, integration) | Killed by EC-83, and by two `PostAppointmentTests` (wrong error code) |
| 3. `ServiceNotOfferedAtDealership` becomes `Conflict` (`mutate.ps1`) | Killed by EC-84 alone |

Note from `domain-analyst`: once the retry after a lost race (EC-18) is built, the retry alone would reach the free pair, so mutations
1 and 2 must be re-run against EC-83 then. `BeyondBookingHorizon` joins EC-84 when rule 5 is built; the package first listed it by mistake.
