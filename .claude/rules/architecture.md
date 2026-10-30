---
paths:
  - "src/**"
---

# Architecture: Clean Architecture + DDD

## Layer responsibilities

| Layer | Owns | Must not |
|---|---|---|
| **Domain** | Aggregates, entities, value objects, domain services, domain errors, invariants | Reference EF Core, ASP.NET Core, logging, `DateTime.Now`, I/O of any kind |
| **Application** | Use-case handlers, ports (`IAppointmentRepository`, `IUnitOfWork`), DTOs for use-case input/output, transaction boundaries | Contain business rules that belong to an aggregate; know about HTTP or SQL |
| **Infrastructure** | `DbContext`, entity configurations, migrations, port implementations, DB error translation | Contain business decisions; leak `DbContext`/`IQueryable` outward |
| **Api** | Endpoint mapping, request/response contracts, input-shape validation, ProblemDetails, DI composition | Contain business logic; return domain entities directly |

The dependency rule is enforced by project references. Never add a reference that points outward.

## DDD tactical patterns used here

- **Aggregates** (consistency boundaries; one repository per aggregate root):
  - `Appointment`: root; owns its time slot, status transitions (`Confirmed → Cancelled`).
  - `Dealership`: root; owns `ServiceBay`s, `Technician`s (+ skills, shifts) and opening hours.
  - `ServiceType`, `Customer`, `Vehicle`: small roots / reference data.
- **Reference other aggregates by ID only** (`TechnicianId`, never a `Technician` navigation on `Appointment` in the domain model).
- **Value objects** are immutable `record`/`readonly record struct` types with validation in the
  factory: `TimeSlot` (half-open interval), `Vin`, `Skill`, `BayType`, `DealershipTimeZone`, strongly typed IDs.
  No primitive obsession: a method taking `(Guid, Guid, DateTime, DateTime)` is a design smell.
- **Domain service** for logic that spans aggregates: `SlotAllocator` decides which (bay, technician)
  pair to use. It is pure: inputs in, decision out.
- **Set-based invariants** (no overlap across *many* appointments) cannot be enforced inside a single
  aggregate. They are enforced by the database (exclusion constraint) and surfaced as a domain error.
  This is a deliberate DDD trade-off; do not try to load "all appointments" into an aggregate to check it.
- **Rich model, not anemic:** state changes go through intention-revealing methods
  (`appointment.Cancel(reason, now)`), never public setters. Constructors/factories guarantee a valid object.

## Use-case shape (Application)

- One handler class per use case: `BookAppointmentHandler`, `GetAvailabilityHandler`, `GetAppointmentHandler`.
  Commands change state; queries never do (CQS).
- Handlers orchestrate: load → call domain → persist → map result. If a handler contains `if` chains
  about business rules, move them into the domain.
- No mediator library. Handlers are plain classes resolved from DI and invoked directly by endpoints.
  (MediatR and AutoMapper are commercially licensed since 2025 and add indirection we do not need.)
- Mapping is explicit (extension methods or constructors). No reflection-based mappers.

## Ubiquitous language

Use the domain terms exactly, in code, tests, logs and docs: *Dealership, ServiceBay, BayType,
Technician, Skill, Shift, OpeningHours, ServiceType, Appointment, TimeSlot, Booking, Availability,
Allocation*. Do not introduce synonyms (`Slot` vs `Booking` vs `Reservation` for the same concept).
