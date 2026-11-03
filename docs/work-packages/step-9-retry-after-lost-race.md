# Work package: step 9, retry with another pair after a lost race (EC-18)

- **Branch:** `feature/allocation`, adding to PR #2
- **Status:** the developer chose this on 2026-11-03. Without it, simultaneous requests are refused while a qualified technician and
  a bay are still free. The advisor never chooses the technician; the system does, so choosing again before answering is invisible
  to the customer. This supersedes ADR-0002 #27.
- **Rules:** `booking-domain.md` *Allocation policy* 3 and 4; ADR-0002 #17; `docs/system-design.md` §7.1 and §7.2.

## Behaviour

- When `AddAsync` fails with `NoQualifiedTechnicianAvailable` or `NoCompatibleBayAvailable`, a concurrent request has taken that
  technician or bay. The handler then:
  - excludes the resource that conflicted;
  - reads the busy intervals again;
  - allocates again;
  - saves again.

  It does this at most `MaxAllocationAttempts` = 3 times in total. When the attempts run out, it returns the capacity error of the
  resource that conflicted **last**.
- `VehicleAlreadyBooked` is never retried; it is returned at once (policy 4).
- If allocation finds no pair on a later attempt, return that capacity error, as on the first attempt.
- The appointment ID stays the same across attempts (`booking-domain.md`, *Idempotency*: generated once before persisting).
- Excluding resources belongs in the domain: `SlotAllocator` takes the excluded technicians and bays, for example on
  `AllocationRequest`. The handler only orchestrates the loop.

## Scenarios

| EC | Suite | Given / When | Then |
|---|---|---|---|
| EC-18 | Unit (handler, fake repository) | 2 technicians, 2 bays. The first save fails with `NoQualifiedTechnicianAvailable` for T1 | Second attempt saved on T2; `201`-equivalent success; T1 never retried |
| EC-44 | Unit | Every save fails, with the first conflict **different** from the last: (technician, bay, bay) and (bay, technician, technician); 3 technicians and 3 bays, so a fourth pair stays free | After 3 attempts, the last conflict's error: `NoCompatibleBayAvailable`, then `NoQualifiedTechnicianAvailable`; exactly 3 saves. The orchestrator's first sequence (technician, bay, technician) had the same first and last conflict and could not catch mutation 3 (`domain-analyst`) |
| EC-90 | Unit | The first save fails with `VehicleAlreadyBooked` | `VehicleAlreadyBooked`; exactly 1 save |
| EC-91 | Unit | The first save fails on bay B1; B2 is free | Saved on B2 |
| EC-85 | Integration (change to a frozen assertion, approved) | 3 technicians, 3 bays, 20 parallel requests | **Exactly 3** `201`s on distinct technicians and bays; 17 `409`; 3 rows; no `500` |
| EC-89 | Integration (change to a frozen assertion, approved) | 2 technicians, 1 bay, 20 parallel requests | Exactly one `201`; every loser `409 NoCompatibleBayAvailable`; 1 row |
| EC-17, EC-74, EC-75 | Integration, unchanged | — | Must still pass: with the stale reader, retries exhaust and return the same codes |

## Mutations

| # | Mutation | Must fail |
|---|---|---|
| 1 | No retry (return the first save error) | EC-18, EC-85, EC-89 |
| 2 | Retry also on `VehicleAlreadyBooked` | EC-90 |
| 3 | Return the first conflict instead of the last | EC-44 |
| 4 | The conflicting resource is not excluded (busy intervals re-read only) | EC-18 or EC-91 with a fake reader that does not see the winner |
| 5 | Attempts not bounded, or bounded at 1 | EC-44 |

Unit mutations go into `tools/mutation/mutate.ps1` where possible. Integration-only mutations are applied by hand and recorded here.

## Approved extras

No NuGet package, no API contract change. The developer approved changing EC-85's and EC-89's assertions to the stronger outcome.

## Results (2026-11-03)

**Red** (re-run by the orchestrator). Unit: 83/87. Integration: 52/54.
- Failing for the right reason: EC-18, EC-91, both EC-44 rows, EC-85 (`Expected: 3 / Actual: 1`) and EC-89 (losers got `NoQualifiedTechnicianAvailable`).
- EC-90 passed at once.
- `domain-analyst` first said "not ready": the EC-44 rows had the same first and last conflict, so mutation 3 could not fail. That came
  from the package. The rows were changed to (technician, bay, bay) and (bay, technician, technician) before the freeze. Fingerprint `425e1157`.

**Green** (re-run by the orchestrator):
- build clean, 87/87 unit, 54/54 integration, format clean;
- race classes 15/15;
- `mutate.ps1` 33/33. Mutations 1–5 are killed by EC-44, EC-90, EC-91 and EC-18; mutation 5 is split into "not bounded" and "bounded at 1".

**Hand mutations** (implementer and `code-reviewer`, on copies):

| Mutation | Killed by |
|---|---|
| Reader returns no busy intervals | EC-29, EC-85. EC-83 no longer catches it |
| Technician busy intervals built from bay IDs | `CapacityTests` overlap theory (3 rows), EC-29. EC-83 no longer catches it |
| Bay busy intervals dropped | EC-85 only |
| No translation of `23P01` | 8 tests |

`code-reviewer`: no blocker. Its one major (the busy-interval query is no longer pinned by EC-83 or EC-86) was deferred by the
developer and recorded in `docs/edge-cases.md`, *Deferred*.
