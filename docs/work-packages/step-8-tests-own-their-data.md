# Work package: step 8, an integration suite written from the business scenarios; a richer demo seed

- **Branch:** `feature/allocation`, adding to PR #2
- **Status:** the developer decided on 2026-11-03.
  - "Tests verify that the code matches the business; they are not defined to fit the data."
  - The integration suite is **rewritten from the business scenarios**. Every test creates its own dealership. No test depends on the seed.
  - Scenario list approved; ADR-0002 #26–28 decided.
  - A first attempt that only reworked the old tests' Arrange was stopped. Its builder (`Fixtures/SchedulingDataBuilder.cs`) and partial
    edits are still in the working tree and may be reused.
- **Sources:** the brief, requirements 1–3; `booking-domain.md`; ADR-0002; the `domain-analyst` scenario list (2026-11-03), summarised below.

## Part A: the new suite (`tests/`)

**Common defaults:**
- "Now" is Mon 2026-11-02 09:00, from the fixture's `FixedTimeProvider`. "Tue" is 2026-11-03.
- The dealership is open Tue 08:00–17:00.
- The oil change takes 60 min, needs `General` skill and a `Standard` bay, buffer 0.
- Each technician has `{General}` and a Tue 08:00–17:00 shift. Each bay is an active `Standard` bay.
- Each test states only what differs from these defaults.

**Rules for every test:**
- A resource that the rule must skip is declared **first**, so the ID tie-break would choose it if the rule were broken.
- Every rejection also asserts that no new row was stored.

### R1: the request and its validation
| # | EC | Given / When | Then |
|---|---|---|---|
| 1 | EC-72 | 1 technician, 1 bay; oil change Tue 10:00 | `201` + Location; T1/B1, 10:00–11:00, `Confirmed`; one row with customer, vehicle, technician, bay, start, end |
| 2 | EC-19 | Unknown customer / vehicle / service type / dealership (Theory, 4 rows) | `404 NotFound`, 0 rows |
| 3 | EC-20 | Customer A books customer B's vehicle | `422 VehicleNotOwnedByCustomer` |
| 4 | EC-22 | EV job; only `{General}` technicians; an EV bay exists | `422 ServiceNotOfferedAtDealership` |
| 5 | EC-22 | The only technician is inactive (a), or the only bay is inactive (b) | `422 ServiceNotOfferedAtDealership`, not 409 |
| 6 | EC-09 | Start exactly now, Mon 09:00 (a); an earlier date on an open day, Tue 2026-10-27 10:00 (b) | `422 StartInPast` |
| 7 | EC-58 | Also open Monday, with a Monday shift; Mon 09:15 (same day) | `201` |
| 8 | EC-10/31/59 | Tue 10:10 and 10:00:30 → `422 StartNotOnGrid`; 10:15 and 10:45 → `201` (Theory) | as stated |
| 9 | EC-03/33 | 07:45, 16:15 (ends 17:15), Wednesday (closed) → `422 OutsideOpeningHours`; 08:00 and 16:00 → `201` (Theory) | as stated |

### R2: a bay and a qualified technician both free for the whole service
| # | EC | Given / When | Then |
|---|---|---|---|
| 10 | EC-73/02/01 | 1 technician, 1 bay; vehicle 1 holds 10:00–11:00. Vehicle 2 at 10:00, 09:30, 10:30 → `409 NoQualifiedTechnicianAvailable`; at 09:00 and 11:00 → `201` (Theory) | as stated |
| 11 | EC-86 | 2 technicians, **1 bay**; vehicle 1 holds T1/B1 10:00; vehicle 2 at 10:00 | `409 NoCompatibleBayAvailable`, 1 row |
| 12 | EC-83 | 2 technicians, 2 bays; car 1 holds T1/B1 10:00; car 2 at 10:30 | `201` T2/B2, 2 rows. The busy technician must be the one best fit would choose: give T2 a second skill |
| 13 | EC-87 | EV job; T1 `{General}`, T2 `{General, EV}`; an EV bay | `201` T2 |
| 14 | EC-88 | T1 shift Tue 09:00–12:00, T2 08:00–17:00. 08:00 and 11:30 → T2; 09:00 and 11:00 → T1 (Theory) | as stated |
| 15 | EC-23/69 | The only technician has a Monday shift only; Tue 10:00 | `409 NoQualifiedTechnicianAvailable`, not 422 |
| 16 | EC-63 | T1 inactive, T2 active; B1 inactive, B2 active | `201` T2/B2 |
| 17 | EC-05 | EV job; 2 EV technicians; B1 Standard, B2 EV; vehicle 1 holds the EV job on B2 at 10:00; vehicle 2 at 10:00 | `409 NoCompatibleBayAvailable`; the Standard bay is not used |
| 18 | EC-13 | A **Cancelled** appointment on T1/B1 at 10:00, stored directly (ADR-0002 #28); 1 technician, 1 bay; Tue 10:00 | `201` T1/B1; 2 rows, 1 `Confirmed` |
| 19 | EC-25 | A 50-min service type; 10:00, then 11:00 | `201` 10:00–10:50, then `201` on T1/B1 |
| 20 | EC-14 | 2 technicians, 2 bays; vehicle 1 holds 10:00–11:00; **the same vehicle** at 10:30 | `409 VehicleAlreadyBooked` (ADR-0002 #26), 1 row. **Red today: 500** |

### Allocation
| # | EC | Given / When | Then |
|---|---|---|---|
| 21 | EC-07/62/66 | Bays declared EV, Lift, Standard (a); EV, Alignment, Lift (b); an oil change | (a) the Standard bay; (b) the Lift bay |
| 22 | EC-08 | T1 `{General, EV}`, T2 `{General}`; oil change | T2 |
| 23 | EC-29 | T1, T2 `{General}`; 1 bay; vehicle 1 booked on T1 at 08:00; vehicle 2 at 10:00 (no overlap) | `201` **T2**: the less-loaded technician (whole-date workload, ADR-0002 #20) |

### Concurrency (gate, 20 requests, each for its own vehicle)
| # | EC | Given | Then |
|---|---|---|---|
| 24 | EC-17 | 1 technician, 1 bay | Exactly one `201`; 19 `409`; 1 row; no `500` |
| 25 | EC-85 | 3 technicians, 3 bays | 1 to 3 `201`s, on distinct technicians and distinct bays; rows equal successes; no `500` (ADR-0002 #27) |
| 26 | EC-89 | 2 technicians, **1 bay** | Exactly one `201`; 1 row; no `500`; losers get either capacity code (ADR-0002 #27) |

### Technical guards (kept, but they must own their data)
- EC-74/75: the stale reader.
- EC-77/78: the lock order.
- EC-79–82: logging.
- EC-84: error kinds. Add the `VehicleAlreadyBooked` row as `Conflict`.

Their assertions stay as they are.

### Replace, don't duplicate
The new booking suite replaces `PostAppointmentTests`, `AnotherFreeResourceTests`, `BookingRaceTests` (EC-17), `BayRaceTests` and the
stopped attempt's `CapacityRaceTests`. Keep every EC listed above exactly once. `grep -rn SeedData tests/` must be empty.

## Part C: `VehicleAlreadyBooked` (`src/`, ADR-0002 #26)
- New `BookingErrors.VehicleAlreadyBooked` (`Conflict`).
- `AppointmentRepository.ToConflictError` (renamed from `ToCapacityError`) maps `ex_appointments_vehicle_overlap` to it.
- Red first: scenario 20.
- Mutation: the vehicle constraint is not translated (scenario 20 must fail). Add it to the hand-applied list.

## Part B: demo seed (done in the worktree `feature/demo-seed`)
A Northgate migration and the `.http` 8-step demo; verified by the implementer with `docker compose`. It is cherry-picked after Part A.

## Verification
- Red: each test's actual result, quoted from the full output. Most will pass at once ("already covered"); scenario 20 fails with `500`.
- `grep -rn SeedData tests/` is empty.
- Build, unit, integration and format pass. The concurrency classes are looped 15 times.
- `mutate.ps1` passes.
- Every earlier hand mutation is re-run once on a fresh copy and must be killed:
  - step 5: 1–4, 6, 7;
  - logging: 1–8 (7b may survive);
  - step 7: 1, 2;
  - Part C.

## Approved extras
No NuGet package. The API contract gains the documented `409 VehicleAlreadyBooked` (already listed in `api-conventions.md`).

## Results (2026-11-03)

**Red** (re-run by the orchestrator). Integration: 53/54. Unit: 73/74. The two failures were the expected ones:
- scenario 20: `Expected: Conflict / Actual: InternalServerError`, from the untranslated vehicle constraint;
- the EC-84 `VehicleAlreadyBooked` row: `NotImplementedException`.

Every other test passed at once ("already covered"). `grep SeedData tests/` is empty, and `domain-analyst` said "ready". Fingerprint
`41767f4242e20b08d661c76d63b75433b2ec9d72`.

**Green, Part C** (re-run by the orchestrator):
- build clean, 82/82 unit, 54/54 integration, format clean;
- fingerprint unchanged, `git diff -- tests/` empty.

**Hand mutations** (implementer, one fresh copy each, the full integration suite):

| Mutation | Result | Killed by |
|---|---|---|
| Step 5, 1. No translation | Killed | Scenario 20, `ConcurrentBookingTests`, `LockOrderRaceTests`, EC-74, EC-75 |
| Step 5, 2. Every `23P01` maps to the technician error | Killed | Scenario 20, EC-77, EC-75 |
| Step 5, 3. Error swallowed, `201` returned | Killed | Same as 1 |
| Step 5, 4. Technician and bay constraints never fire | Killed | EC-17, EC-85, EC-89, EC-77, EC-78, EC-74, EC-75 |
| Step 5, 6. Bay lock removed | Killed | EC-78, EC-77 |
| Step 5, 7. Technician lock removed | Killed | EC-77 |
| Logging 1–6, 7a, 8 | Killed | EC-79 to EC-82 as before |
| Logging 7b | Survives, as recorded in `step-4-logging.md` | — |
| Step 7, 1. Reader returns no busy intervals | Killed | EC-29, EC-86, EC-83, EC-05, scenario 20 |
| Step 7, 2. Technician busy intervals built from bay IDs | Killed | EC-29, EC-83, EC-73/02, scenario 20 |
| Part C. Vehicle constraint not translated | Killed | Scenario 20 |
| Bay busy intervals built from technician IDs (mirror of step 7, 2) | Killed | EC-83, scenario 20. EC-86 and EC-05 pass under it, because the bay constraint gives the same answer |

Compared with the old suite, step 7's mutation 1 (the reader ignores bookings) is now caught by five tests instead of one.
