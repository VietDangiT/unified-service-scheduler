# System Design: Unified Service Scheduler

| | |
|---|---|
| **Scenario** | A: The Unified Service Scheduler (Ownership domain) |
| **Implemented layer** | Backend (REST API + PostgreSQL). Client represented by an OpenAPI contract, a `.http` file and an optional thin demo page |
| **Status** | v1.0. Assumptions in [ADR-0002](adr/0002-scope-and-business-assumptions.md) *Accepted* provisionally, to be revisited after testing; edge cases in the [catalogue](edge-cases.md) |

**Contents**

1. [Problem and scope](#1-problem-and-scope)
2. [Assumptions](#2-assumptions)
3. [Architecture (C4 model)](#3-architecture-c4-model)
4. [Components and responsibilities](#4-components-and-responsibilities)
5. [Domain model (class diagrams)](#5-domain-model-class-diagrams)
6. [Data model (database diagram)](#6-data-model-database-diagram)
7. [Data flow (sequence diagrams)](#7-data-flow-sequence-diagrams)
8. [Concurrency and consistency](#8-concurrency-and-consistency)
9. [API contract](#9-api-contract)
10. [Technology choices](#10-technology-choices)
11. [Observability](#11-observability)
12. [Scalability, performance and reliability](#12-scalability-performance-and-reliability)
13. [How GenAI was used in the design phase](#13-how-genai-was-used-in-the-design-phase)
14. [Future work](#14-future-work)

---

## 1. Problem and scope

### Business problem

Dealership service departments still book work by phone, paper and spreadsheets. The result is
double-booked bays, jobs given to technicians who are not certified for them, and idle capacity
elsewhere in the day. Every lost workshop hour is lost revenue.

### Requirements (from the brief)

| # | Requirement | How this design meets it |
|---|---|---|
| R1 | **Resource-constrained booking**: request an appointment for a vehicle, service type and dealership at a desired time | `POST /api/appointments` with `customerId`, `vehicleId`, `serviceTypeId`, `dealershipId`, `requestedStart` |
| R2 | **Real-time availability check** of both a service bay **and** a qualified technician for the **entire** duration | Synchronous allocation at request time; whole-interval checks against shifts, opening hours and existing bookings; final guarantee by database exclusion constraints |
| R3 | **Confirmed appointment record** associating customer, vehicle, technician and bay | `appointments` row with all four foreign keys, status `Confirmed`, stored UTC interval |

### Non-functional goals

- **Correct under concurrency**: no double-booking even with many simultaneous requests and several API instances.
- **Explainable rejections**: the advisor learns *why* a slot failed and gets alternatives.
- **Observable**: every booking decision can be traced and measured.
- **Simple to run**: `docker compose up`; tests run against real PostgreSQL.

### Out of scope

Authentication, notifications, payments, rescheduling, multi-technician or multi-day jobs, technician
leave, customer self-service portal. See [§14](#14-future-work) for how the design accommodates them.

---

## 2. Assumptions

Full rationale in [ADR-0002](adr/0002-scope-and-business-assumptions.md).

| Area | Assumption |
|---|---|
| User | A **service advisor** books on behalf of the customer through an internal tool |
| Allocation | The **system** picks technician and bay; the advisor does not. **Best fit** for both: least specialised bay, then technician with the fewest skills, then least loaded |
| Unavailable time | `409` with the reason and the **earliest 3 free start times** after the requested one, up to **7 days** ahead |
| Never possible | No compatible bay or no skilled technician at the dealership at all → `422 ServiceNotOfferedAtDealership` |
| Duration | Service duration from the service type, plus an optional buffer (default 0). The **service** must fit opening hours and the shift; the **buffer** may run past closing and blocks only technician and bay. Both instants stored at booking time |
| Granularity | Start times on a **15-minute grid** of the local wall clock |
| Horizon | Bookings up to **90 days** ahead |
| Calendars | One opening range per weekday per dealership; one shift range per weekday per technician; no leave, holidays, split shifts or breaks |
| Bay compatibility | A `Standard` job can use any bay; `Lift`, `EV` and `Alignment` jobs need that bay type |
| Lifecycle | `Confirmed → Cancelled` only; cancel is idempotent and refused once the appointment has started |
| Time | Stored in UTC; opening hours and shifts evaluated in the dealership's IANA time zone; appointments do not span local midnight |
| Reference data | Customers, vehicles, dealerships, bays, technicians and service types are seeded locally; in production they would be synchronised from the Dealer Management System |

---

## 3. Architecture (C4 model)

The [C4 model](https://c4model.com) describes the system at four zoom levels. Each level below zooms
into one box of the level above. Diagrams use C4 notation and colours, drawn with Mermaid.

**Legend:** dark blue = person · blue = this system / its containers · light blue = components ·
grey = external system · dashed = future, not built in this assessment.

### Level 1: System context

Who uses the scheduler and what it talks to.

```mermaid
flowchart TB
    advisor["<b>Service Advisor</b><br/>[Person]<br/>Books service appointments<br/>for dealership customers"]
    manager["<b>Workshop Manager</b><br/>[Person]<br/>Monitors capacity and<br/>booking health"]

    scheduler["<b>Unified Service Scheduler</b><br/>[Software System]<br/>Allocates a qualified technician<br/>and a compatible bay for the whole<br/>service duration, and records the appointment"]

    dms["<b>Dealer Management System</b><br/>[External System]<br/>Master data: customers, vehicles,<br/>staff, service catalogue"]
    notify["<b>Notification Service</b><br/>[External System]<br/>Customer confirmations<br/>by SMS or email"]

    advisor -- "Requests appointments,<br/>checks availability<br/>[HTTPS/JSON]" --> scheduler
    manager -- "Views dashboards<br/>and metrics" --> scheduler
    dms -. "Synchronises reference data<br/>[future]" .-> scheduler
    scheduler -. "Publishes AppointmentBooked<br/>[future]" .-> notify

    classDef person fill:#08427b,stroke:#052e56,color:#fff
    classDef system fill:#1168bd,stroke:#0b4884,color:#fff
    classDef external fill:#999999,stroke:#6b6b6b,color:#fff
    class advisor,manager person
    class scheduler system
    class dms,notify external
```

### Level 2: Containers

The separately deployable or runnable parts of the system.

```mermaid
flowchart TB
    advisor["<b>Service Advisor</b><br/>[Person]"]

    subgraph boundary["Unified Service Scheduler [Software System]"]
        client["<b>Advisor Client</b><br/>[Container: OpenAPI UI / .http file /<br/>optional React page]<br/>Stub client for the API"]
        api["<b>Scheduling API</b><br/>[Container: ASP.NET Core 10, minimal APIs]<br/>Validates requests, allocates resources,<br/>enforces business rules, exposes REST"]
        db[("<b>Scheduling Database</b><br/>[Container: PostgreSQL 16]<br/>Reference data and appointments.<br/>Exclusion constraints prevent overlap")]
        otel["<b>Telemetry Dashboard</b><br/>[Container: .NET Aspire Dashboard, OTLP]<br/>Logs, traces and metrics"]
    end

    advisor -- "Uses" --> client
    client -- "Calls<br/>[HTTPS/JSON]" --> api
    api -- "Reads and writes<br/>[EF Core / Npgsql, TCP 5432]" --> db
    api -- "Exports logs, traces, metrics<br/>[OTLP/gRPC]" --> otel

    classDef person fill:#08427b,stroke:#052e56,color:#fff
    classDef container fill:#438dd5,stroke:#2e6295,color:#fff
    class advisor person
    class client,api,db,otel container
    style boundary fill:none,stroke:#1168bd,stroke-dasharray:5 5
```

| Container | Technology | Responsibility | Scales by |
|---|---|---|---|
| Advisor Client | OpenAPI UI, `.http`, optional React | Drive the API for demos and manual testing (stubbed client layer) | n/a |
| Scheduling API | .NET 10, ASP.NET Core minimal APIs | All business logic; stateless | Adding instances behind a load balancer |
| Scheduling Database | PostgreSQL 16 + `btree_gist` | Source of truth; enforces no-overlap invariants | Vertical scale, then read replicas / partitioning |
| Telemetry Dashboard | .NET Aspire Dashboard (OTLP) | Local observability; swappable for Azure Monitor / Grafana | n/a |

### Level 3: Components (inside the Scheduling API)

```mermaid
flowchart TB
    client["<b>Advisor Client</b><br/>[Container]"]
    db[("<b>Scheduling Database</b><br/>[Container: PostgreSQL]")]

    subgraph apiBoundary["Scheduling API [Container]"]
        direction TB
        subgraph apiLayer["Api layer"]
            endpoints["<b>Appointment and Availability Endpoints</b><br/>[Component: Minimal API route groups]<br/>Bind and shape-validate requests,<br/>call handlers, map results"]
            errors["<b>Error Mapper and Exception Handler</b><br/>[Component: IExceptionHandler]<br/>Domain error to ProblemDetails,<br/>unhandled exception to 500"]
        end
        subgraph appLayer["Application layer"]
            book["<b>BookAppointmentHandler</b><br/>[Component]<br/>Idempotency, validation, allocation,<br/>persistence with conflict retry"]
            avail["<b>GetAvailabilityHandler</b><br/>[Component]<br/>Free start times for a day<br/>and service type"]
        end
        subgraph domainLayer["Domain layer"]
            allocator["<b>SlotAllocator</b><br/>[Component: Domain service]<br/>Chooses bay and technician<br/>using allocation policies"]
            model["<b>Booking Model</b><br/>[Component: Aggregates, value objects]<br/>Appointment, TimeSlot, Dealership,<br/>Technician, ServiceBay, ServiceType"]
        end
        subgraph infraLayer["Infrastructure layer"]
            reader["<b>SchedulingReader</b><br/>[Component: EF Core projections]<br/>Booking context and busy intervals<br/>in a bounded number of queries"]
            repo["<b>AppointmentRepository</b><br/>[Component: EF Core]<br/>Persists appointments; translates<br/>23P01 / 23505 to domain conflicts"]
            dbctx["<b>SchedulingDbContext</b><br/>[Component: EF Core + Npgsql]<br/>Mappings, migrations, constraints"]
        end
        telemetry["<b>Telemetry</b><br/>[Component: OpenTelemetry]<br/>ActivitySource and Meter 'ServiceScheduler'"]
    end

    client -- "HTTPS/JSON" --> endpoints
    endpoints --> book
    endpoints --> avail
    endpoints --> errors
    book --> allocator
    avail --> allocator
    allocator --> model
    book --> model
    book -- "IAppointmentRepository" --> repo
    book -- "ISchedulingReader" --> reader
    avail -- "ISchedulingReader" --> reader
    reader --> dbctx
    repo --> dbctx
    dbctx -- "SQL" --> db
    book -.-> telemetry

    classDef container fill:#438dd5,stroke:#2e6295,color:#fff
    classDef component fill:#85bbf0,stroke:#5d82a8,color:#000
    class client,db container
    class endpoints,errors,book,avail,allocator,model,reader,repo,dbctx,telemetry component
    style apiBoundary fill:none,stroke:#438dd5,stroke-dasharray:5 5
```

Dependency rule: arrows between layers point inward only. Application talks to Infrastructure
**through interfaces it owns** (`ISchedulingReader`, `IAppointmentRepository`); Infrastructure
implements them. The Domain layer has no dependencies.

### Level 4: Code

Covered by the class diagrams in [§5](#5-domain-model-class-diagrams).

### Deployment view

```mermaid
flowchart LR
    subgraph local["Local / review: docker compose"]
        apiC["scheduler-api<br/>[Docker container]"]
        pgC[("postgres:16<br/>[Docker container]")]
        aspC["aspire-dashboard<br/>[Docker container]"]
        apiC --> pgC
        apiC --> aspC
    end
    subgraph azure["Target production (illustrative): Azure"]
        aca["Azure Container Apps<br/>N x scheduler-api"]
        pgf[("Azure Database for PostgreSQL<br/>Flexible Server, btree_gist enabled")]
        mon["Azure Monitor /<br/>Application Insights"]
        aca --> pgf
        aca --> mon
    end
```

The API is stateless, so production scales by running N instances. Correctness does not depend on
instance count because the overlap guarantee lives in the database ([§8](#8-concurrency-and-consistency)).

---

## 4. Components and responsibilities

| Component | Layer | Responsibility | Must not |
|---|---|---|---|
| Endpoints | Api | Route, bind, shape-validate, call one handler, map `Result` to HTTP | Contain business rules |
| Error Mapper / Exception Handler | Api | Single mapping of domain errors to status codes and RFC 9457 ProblemDetails; generic 500 for unexpected errors | Leak stack traces or exception messages |
| `BookAppointmentHandler` | Application | Orchestrate a booking: idempotency check → load context → domain validation → allocate → persist → retry on conflict | Decide business rules itself (delegates to Domain) |
| `GetAvailabilityHandler` | Application | Compute free 15-minute start times for one dealership-day and service type | Query per slot (N+1) |
| `SlotAllocator` | Domain | Pure decision: given candidates and busy intervals, pick a (bay, technician) pair or return a typed capacity error | Perform I/O |
| Allocation policies | Domain | `BestFitBayPolicy`, `BestFitTechnicianPolicy` (Strategy pattern) | Depend on infrastructure |
| Booking model | Domain | Aggregates and value objects that are valid by construction (`TimeSlot`, `Appointment.Book`) | Expose public setters |
| `SchedulingReader` | Infrastructure | Read-only projections (`AsNoTracking`) for booking context and busy intervals | Return `IQueryable` or tracked entities |
| `AppointmentRepository` | Infrastructure | Insert/load appointments; translate PostgreSQL `23P01` (overlap) and `23505` (duplicate idempotency key) into domain conflicts | Let provider exceptions escape |
| `SchedulingDbContext` | Infrastructure | EF Core mappings, migrations, constraints, seed data | Be shared across threads |
| Telemetry | Cross-cutting | Traces, metrics and structured logs exported via OTLP | Record personal data or high-cardinality tags |

---

## 5. Domain model (class diagrams)

### 5.1 Domain layer

```mermaid
classDiagram
    direction LR

    class Appointment {
        <<Aggregate Root>>
        +AppointmentId Id
        +DealershipId DealershipId
        +CustomerId CustomerId
        +VehicleId VehicleId
        +ServiceTypeId ServiceTypeId
        +TechnicianId TechnicianId
        +ServiceBayId ServiceBayId
        +TimeSlot Slot
        +DateTimeOffset BlockedUntilUtc
        +AppointmentStatus Status
        +IdempotencyKey? IdempotencyKey
        +DateTimeOffset CreatedAtUtc
        +DateTimeOffset? CancelledAtUtc
        +Book(id, request, allocation, now)$ Result~Appointment~
        +Cancel(now) Result
    }

    class AppointmentStatus {
        <<enumeration>>
        Confirmed
        Cancelled
    }

    class TimeSlot {
        <<Value Object>>
        +DateTimeOffset StartUtc
        +DateTimeOffset EndUtc
        +Create(start, duration)$ TimeSlot
        +Overlaps(TimeSlot other) bool
        +IsAlignedTo(TimeSpan grid) bool
    }

    class Dealership {
        <<Aggregate Root>>
        +DealershipId Id
        +string Name
        +TimeZoneInfo TimeZone
        +WeeklySchedule OpeningHours
        +IReadOnlyCollection~ServiceBay~ Bays
        +IReadOnlyCollection~Technician~ Technicians
        +IsOpenFor(TimeSlot slot) bool
    }

    class ServiceBay {
        <<Entity>>
        +ServiceBayId Id
        +string Name
        +BayType Type
        +bool IsActive
        +CanServe(BayType required) bool
    }

    class Technician {
        <<Entity>>
        +TechnicianId Id
        +string Name
        +IReadOnlySet~Skill~ Skills
        +WeeklySchedule Shifts
        +bool IsActive
        +IsQualifiedFor(Skill skill) bool
        +IsOnShiftFor(TimeSlot slot, TimeZoneInfo zone) bool
    }

    class WeeklySchedule {
        <<Value Object>>
        +IReadOnlyDictionary~DayOfWeek, LocalTimeRange~ Days
        +Covers(TimeSlot slot, TimeZoneInfo zone) bool
    }

    class ServiceType {
        <<Aggregate Root>>
        +ServiceTypeId Id
        +string Code
        +string Name
        +TimeSpan Duration
        +TimeSpan Buffer
        +Skill RequiredSkill
        +BayType RequiredBayType
        +TotalDuration() TimeSpan
    }

    class BayType {
        <<enumeration>>
        Standard
        Lift
        EV
        Alignment
    }

    class Skill {
        <<Value Object>>
        +string Code
    }

    class SlotAllocator {
        <<Domain Service>>
        -IBayAllocationPolicy bayPolicy
        -ITechnicianAllocationPolicy technicianPolicy
        +Allocate(AllocationRequest request) Result~Allocation~
    }

    class IBayAllocationPolicy {
        <<interface>>
        +Choose(candidates, required) ServiceBay?
    }
    class ITechnicianAllocationPolicy {
        <<interface>>
        +Choose(candidates, workload) Technician?
    }
    class BestFitBayPolicy {
        least specialised bay, then ID
    }
    class BestFitTechnicianPolicy {
        fewest skills, then least loaded, then ID
    }

    class Allocation {
        <<Value Object>>
        +ServiceBayId BayId
        +TechnicianId TechnicianId
        +TimeSlot Slot
    }

    class DomainError {
        <<Value Object>>
        +string Code
        +string Message
        +ErrorKind Kind
    }

    Appointment --> TimeSlot
    Appointment --> AppointmentStatus
    Dealership "1" *-- "many" ServiceBay
    Dealership "1" *-- "many" Technician
    Dealership --> WeeklySchedule : opening hours
    Technician --> WeeklySchedule : shifts
    Technician --> "many" Skill
    ServiceBay --> BayType
    ServiceType --> Skill
    ServiceType --> BayType
    SlotAllocator --> IBayAllocationPolicy
    SlotAllocator --> ITechnicianAllocationPolicy
    BestFitBayPolicy ..|> IBayAllocationPolicy
    BestFitTechnicianPolicy ..|> ITechnicianAllocationPolicy
    SlotAllocator ..> Allocation : creates
    SlotAllocator ..> DomainError : returns on failure
    Appointment ..> Allocation : booked from
```

Notes:

- `Appointment` references other aggregates **by ID only**, so it can be loaded and saved on its own.
- "No two confirmed appointments overlap for the same technician, bay or vehicle" is a **set-based
  invariant** across many `Appointment` aggregates. It cannot be enforced inside one aggregate without
  loading all others, so it is enforced by the database and surfaced as a `DomainError` ([§8](#8-concurrency-and-consistency)).
- Expected failures are returned as `Result<T>` carrying a `DomainError`; exceptions are reserved for faults.
  `TimeSlot.Create` therefore throws on a non-positive duration: durations come from configured service
  types, so that is a programming error, not a business rejection.
- `Slot` is the **service** interval (must fit opening hours and the shift; occupies the vehicle).
  `BlockedUntilUtc` adds the buffer (occupies the technician and bay).
- The appointment ID is a `Guid.CreateVersion7()` created by the application before persisting, so a
  write whose outcome is unknown can be checked by ID ([§8](#8-concurrency-and-consistency)).

### 5.2 Application and infrastructure

```mermaid
classDiagram
    direction LR

    class BookAppointmentHandler {
        -ISchedulingReader reader
        -IAppointmentRepository repository
        -SlotAllocator allocator
        -TimeProvider clock
        -BookingOptions options
        +HandleAsync(BookAppointmentCommand, CancellationToken) Task~Result~AppointmentDto~~
    }

    class GetAvailabilityHandler {
        -ISchedulingReader reader
        -SlotAllocator allocator
        -TimeProvider clock
        +HandleAsync(GetAvailabilityQuery, CancellationToken) Task~Result~AvailabilityDto~~
    }

    class BookAppointmentCommand {
        <<record>>
        +Guid CustomerId
        +Guid VehicleId
        +Guid ServiceTypeId
        +Guid DealershipId
        +DateTimeOffset RequestedStart
        +string? IdempotencyKey
    }

    class ISchedulingReader {
        <<interface>>
        +GetBookingContextAsync(command, ct) Task~BookingContext?~
        +GetBusyIntervalsAsync(dealershipId, vehicleId, window, ct) Task~BusyIntervals~
    }

    class IAppointmentRepository {
        <<interface>>
        +FindByIdempotencyKeyAsync(key, ct) Task~Appointment?~
        +GetByIdAsync(id, ct) Task~Appointment?~
        +TryAddAsync(appointment, ct) Task~AddOutcome~
        +TryCancelAsync(id, now, ct) Task~CancelOutcome~
    }

    class AddOutcome {
        <<union>>
        Added
        ResourceConflict(ConflictedResource)
        DuplicateIdempotencyKey
    }

    class BookingOptions {
        +int SlotGranularityMinutes = 15
        +int MaxAllocationAttempts = 3
        +int AlternativesToSuggest = 3
        +int AlternativesSearchDays = 7
        +int BookingHorizonDays = 90
    }

    class SchedulingReader {
        -SchedulingDbContext db
    }
    class AppointmentRepository {
        -SchedulingDbContext db
        -TranslateConflict(PostgresException) AddOutcome
        -ExistsAsync(id, ct) Task~bool~
    }
    class SchedulingDbContext {
        +DbSet~Appointment~ Appointments
        +DbSet~Dealership~ Dealerships
        +DbSet~ServiceType~ ServiceTypes
    }

    BookAppointmentHandler ..> BookAppointmentCommand : handles
    BookAppointmentHandler --> ISchedulingReader
    BookAppointmentHandler --> IAppointmentRepository
    BookAppointmentHandler --> BookingOptions
    GetAvailabilityHandler --> ISchedulingReader
    IAppointmentRepository ..> AddOutcome : returns
    SchedulingReader ..|> ISchedulingReader
    AppointmentRepository ..|> IAppointmentRepository
    SchedulingReader --> SchedulingDbContext
    AppointmentRepository --> SchedulingDbContext
```

---

## 6. Data model (database diagram)

### 6.1 Entity-relationship diagram

```mermaid
erDiagram
    DEALERSHIPS ||--o{ OPENING_HOURS : "opens on"
    DEALERSHIPS ||--o{ SERVICE_BAYS : has
    DEALERSHIPS ||--o{ TECHNICIANS : employs
    TECHNICIANS ||--o{ TECHNICIAN_SKILLS : holds
    TECHNICIANS ||--o{ TECHNICIAN_SHIFTS : works
    CUSTOMERS ||--o{ VEHICLES : owns
    DEALERSHIPS ||--o{ APPOINTMENTS : hosts
    CUSTOMERS ||--o{ APPOINTMENTS : books
    VEHICLES ||--o{ APPOINTMENTS : "is serviced in"
    SERVICE_TYPES ||--o{ APPOINTMENTS : "is performed in"
    TECHNICIANS ||--o{ APPOINTMENTS : "is assigned"
    SERVICE_BAYS ||--o{ APPOINTMENTS : "is reserved by"

    DEALERSHIPS {
        uuid id PK
        text name
        text time_zone "IANA, e.g. Europe/London"
    }
    OPENING_HOURS {
        uuid dealership_id PK, FK
        smallint day_of_week PK "0 = Sunday; no row = closed"
        time opens_at "local time"
        time closes_at "local time, > opens_at"
    }
    SERVICE_BAYS {
        uuid id PK
        uuid dealership_id FK "UK (id, dealership_id)"
        text name
        text bay_type "Standard | Lift | EV | Alignment"
        boolean is_active
    }
    TECHNICIANS {
        uuid id PK
        uuid dealership_id FK "UK (id, dealership_id)"
        text name
        boolean is_active
    }
    TECHNICIAN_SKILLS {
        uuid technician_id PK, FK
        text skill PK "e.g. General, EV, Alignment"
    }
    TECHNICIAN_SHIFTS {
        uuid technician_id PK, FK
        smallint day_of_week PK "no row = off"
        time starts_at "local time"
        time ends_at "local time, > starts_at"
    }
    SERVICE_TYPES {
        uuid id PK
        text code UK
        text name
        int duration_minutes "> 0"
        int buffer_minutes ">= 0"
        text required_skill
        text required_bay_type
    }
    CUSTOMERS {
        uuid id PK
        text full_name "personal data"
        text email "personal data"
        text phone "personal data"
    }
    VEHICLES {
        uuid id PK
        uuid customer_id FK
        text vin UK "17 chars"
        text make
        text model
        smallint model_year
    }
    APPOINTMENTS {
        uuid id PK "UUID v7, generated by the app"
        uuid dealership_id FK
        uuid customer_id FK
        uuid vehicle_id FK
        uuid service_type_id FK
        uuid technician_id FK "with dealership_id"
        uuid service_bay_id FK "with dealership_id"
        timestamptz start_utc
        timestamptz end_utc "start + duration (service)"
        timestamptz blocked_until_utc "end + buffer"
        text status "Confirmed | Cancelled"
        text idempotency_key UK "nullable"
        text request_hash "SHA-256 of the request"
        timestamptz created_at_utc
        timestamptz cancelled_at_utc "nullable"
    }
```

### 6.2 Constraints and indexes that carry business rules

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;

ALTER TABLE appointments
  ADD CONSTRAINT ck_appointments_interval CHECK (end_utc > start_utc AND blocked_until_utc >= end_utc),
  ADD CONSTRAINT ck_appointments_status   CHECK (status IN ('Confirmed', 'Cancelled'));

ALTER TABLE opening_hours     ADD CONSTRAINT ck_opening_hours_range     CHECK (closes_at > opens_at);
ALTER TABLE technician_shifts ADD CONSTRAINT ck_technician_shifts_range CHECK (ends_at > starts_at);

-- A technician or bay can only be assigned within its own dealership.
ALTER TABLE technicians  ADD CONSTRAINT uq_technicians_id_dealership  UNIQUE (id, dealership_id);
ALTER TABLE service_bays ADD CONSTRAINT uq_service_bays_id_dealership UNIQUE (id, dealership_id);
ALTER TABLE appointments
  ADD CONSTRAINT fk_appointments_technician_same_dealership
    FOREIGN KEY (technician_id, dealership_id) REFERENCES technicians (id, dealership_id),
  ADD CONSTRAINT fk_appointments_bay_same_dealership
    FOREIGN KEY (service_bay_id, dealership_id) REFERENCES service_bays (id, dealership_id);

-- No double-booking: one confirmed appointment per resource at any instant.
-- '[)' = half-open, so 10:00-11:00 and 11:00-12:00 do not conflict.
-- Technician and bay are blocked until the buffer ends; the vehicle only for the service itself.
ALTER TABLE appointments ADD CONSTRAINT ex_appointments_technician_overlap
  EXCLUDE USING gist (technician_id WITH =, tstzrange(start_utc, blocked_until_utc, '[)') WITH &&)
  WHERE (status = 'Confirmed');

ALTER TABLE appointments ADD CONSTRAINT ex_appointments_bay_overlap
  EXCLUDE USING gist (service_bay_id WITH =, tstzrange(start_utc, blocked_until_utc, '[)') WITH &&)
  WHERE (status = 'Confirmed');

ALTER TABLE appointments ADD CONSTRAINT ex_appointments_vehicle_overlap
  EXCLUDE USING gist (vehicle_id WITH =, tstzrange(start_utc, end_utc, '[)') WITH &&)
  WHERE (status = 'Confirmed');

-- Idempotent retries: a key can create at most one appointment.
CREATE UNIQUE INDEX ux_appointments_idempotency_key
  ON appointments (idempotency_key) WHERE idempotency_key IS NOT NULL;

-- Availability for one dealership-day.
CREATE INDEX ix_appointments_dealership_start
  ON appointments (dealership_id, start_utc) WHERE status = 'Confirmed';
```

| Rule | Enforced by | Error surfaced |
|---|---|---|
| Technician not double-booked | `ex_appointments_technician_overlap` | `23P01` → retry with next technician, else `409 NoQualifiedTechnicianAvailable` |
| Bay not double-booked | `ex_appointments_bay_overlap` | `23P01` → retry with next bay, else `409 NoCompatibleBayAvailable` |
| Vehicle not in two places | `ex_appointments_vehicle_overlap` | `23P01` → `409 VehicleAlreadyBooked` (no retry) |
| One booking per idempotency key | `ux_appointments_idempotency_key` | `23505` → return the existing appointment |
| Technician and bay belong to the booking's dealership | Composite foreign keys | `23503`: bug if hit; logged as `Error` |
| Valid interval, status, opening hours and shifts | `CHECK` constraints | `23514`: bug or bad seed data; logged as `Error` |

Design notes:

- Each exclusion constraint creates a **GiST index** that also speeds up the overlap queries used by
  availability, so no extra index is needed for them.
- `start_utc` / `end_utc` are `timestamptz`, always UTC. Local time appears only in `opening_hours`
  and `technician_shifts` (`time` columns interpreted in the dealership's zone).
- `end_utc` and `blocked_until_utc` are **stored**, not computed from the service type, so changing a
  service type never rewrites history.
- `time` columns accept `24:00` in PostgreSQL but .NET `TimeOnly` cannot represent it; the `CHECK`
  constraints (`closes_at > opens_at`) make such rows impossible, so ranges never cross midnight.
- Personal data is confined to `customers`; logs reference IDs only.

---

## 7. Data flow (sequence diagrams)

### 7.1 Book an appointment (with idempotency and conflict retry)

```mermaid
sequenceDiagram
    autonumber
    actor Advisor as Service Advisor
    participant EP as Appointments Endpoint
    participant H as BookAppointmentHandler
    participant RD as SchedulingReader
    participant AL as SlotAllocator
    participant RP as AppointmentRepository
    participant DB as PostgreSQL

    Advisor->>EP: POST /api/appointments (Idempotency-Key header)
    EP->>EP: Shape validation (required fields, offset present, key length)
    EP->>H: HandleAsync(command, ct)

    opt Idempotency-Key present
        H->>RP: FindByIdempotencyKeyAsync(key)
        RP->>DB: SELECT appointment WHERE idempotency_key = key
        alt Same key and same request hash
            H-->>EP: Existing appointment, current state (replay)
            EP-->>Advisor: 201 Created (stop)
        else Same key and different request hash
            H-->>EP: IdempotencyKeyReused
            EP-->>Advisor: 422 ProblemDetails (stop)
        end
    end

    H->>RD: GetBookingContextAsync(command)
    RD->>DB: Customer, vehicle, service type, dealership, candidate resources (AsNoTracking)
    alt Any reference missing
        H-->>EP: NotFound
        EP-->>Advisor: 404 ProblemDetails (stop)
    end
    H->>H: Validate in order: vehicle owner, service offered, past, horizon, grid, opening hours
    H->>RD: GetBusyIntervalsAsync(dealership, vehicle, window)
    RD->>DB: Busy intervals of candidates (this dealership) and of the vehicle (all dealerships)
    H->>H: Vehicle free for the service interval? Otherwise VehicleAlreadyBooked
    H->>H: id = Guid.CreateVersion7()

    loop Up to MaxAllocationAttempts (3)
        H->>AL: Allocate(slot, candidates, busy, excluded)
        alt No eligible pair
            AL-->>H: NoQualifiedTechnicianAvailable or NoCompatibleBayAvailable
        else Pair found
            AL-->>H: Allocation(bay, technician)
            H->>RP: TryAddAsync(Appointment.Book(id, ...))
            RP->>DB: INSERT INTO appointments
            alt Insert succeeds
                DB-->>RP: committed
                RP-->>H: Added
            else Exclusion violation 23P01 on technician or bay
                DB-->>RP: constraint name identifies the resource
                RP-->>H: ResourceConflict(resource)
                H->>H: Exclude resource, record retry metric, try again
            else Exclusion violation 23P01 on vehicle
                RP-->>H: ResourceConflict(vehicle), no retry
            else Connection lost, commit outcome unknown
                RP->>DB: SELECT 1 WHERE id = id
                RP-->>H: Added if the row exists, otherwise rethrow
            end
        end
    end

    alt Booked
        H-->>EP: AppointmentDto
        EP-->>Advisor: 201 Created, Location header
    else Capacity error
        H->>AL: Earliest 3 free starts after the request, within 7 days
        H-->>EP: DomainError with alternatives
        EP-->>Advisor: 409 ProblemDetails (reason, alternatives, traceId)
    end
```

The checks run in a fixed order (see `booking-domain.md`), so a request that breaks several rules
always gets the same error. Cheap in-memory checks run before the busy-interval query.
The alternatives search reads busy intervals for the 7-day window in **one** query.

### 7.2 Two advisors race for the last slot

The application-level check passes for both requests because neither has committed yet. The database
decides.

```mermaid
sequenceDiagram
    autonumber
    actor A as Advisor A
    actor B as Advisor B
    participant API1 as API instance 1
    participant API2 as API instance 2
    participant DB as PostgreSQL

    par Same slot requested at the same moment
        A->>API1: Book Oil Change at 10:00
    and
        B->>API2: Book Oil Change at 10:00
    end

    API1->>DB: Read busy intervals
    API2->>DB: Read busy intervals
    Note over API1,API2: Both see technician Alice and Bay-1 as free

    API1->>DB: INSERT (Alice, Bay-1, 10:00-11:00)
    API2->>DB: INSERT (Alice, Bay-1, 10:00-11:00)
    DB-->>API1: Committed
    DB-->>API2: 23P01 ex_appointments_technician_overlap

    API2->>API2: Exclude Alice, re-run allocation
    alt Another qualified technician and bay are free
        API2->>DB: INSERT (Bob, Bay-2, 10:00-11:00)
        DB-->>API2: Committed
        API2-->>B: 201 Created (Bob, Bay-2)
    else No capacity left
        API2-->>B: 409 NoQualifiedTechnicianAvailable + alternatives
    end
    API1-->>A: 201 Created (Alice, Bay-1)
```

### 7.3 Get availability for a day

```mermaid
sequenceDiagram
    autonumber
    actor Advisor as Service Advisor
    participant EP as Availability Endpoint
    participant H as GetAvailabilityHandler
    participant RD as SchedulingReader
    participant AL as SlotAllocator
    participant DB as PostgreSQL

    Advisor->>EP: GET /api/dealerships/{id}/availability?serviceTypeId&date
    EP->>H: HandleAsync(query, ct)
    H->>H: date not in the past, within the horizon (else 422)
    H->>RD: Opening hours, qualified technicians, compatible bays
    RD->>DB: Reference data (AsNoTracking)
    H->>RD: Busy intervals for that dealership-day
    RD->>DB: One range query using the GiST index
    H->>H: Generate starts in elapsed 15-min steps, mapped to local time, keep those after now whose service fits opening hours
    loop Each candidate start (in memory, no extra queries)
        H->>AL: Allocate(slot, candidates, busy)
    end
    H-->>EP: Available start times
    EP-->>Advisor: 200 OK
```

Availability is computed from **two bounded queries** per request and then evaluated in memory
(about 40 starts × a handful of resources per day). It is a snapshot; the booking path re-checks
and the database constraint has the final word.

---

## 8. Concurrency and consistency

| Concern | Mechanism | Why this one |
|---|---|---|
| Double-booking under concurrent requests | PostgreSQL exclusion constraints ([§6.2](#62-constraints-and-indexes-that-carry-business-rules)) | Correct for any number of API instances and any future code path; see [ADR-0001](adr/0001-database-enforces-no-double-booking.md) for rejected alternatives |
| Losing a race | Translate `23P01` → exclude the conflicted resource → re-allocate (max 3) | The advisor still gets a booking when capacity exists elsewhere |
| Client retries after a timeout | `Idempotency-Key` + unique partial index + request hash | Exactly-once booking from the client's view |
| Two concurrent requests with the same key | Second insert hits `23505` → load and return the first | No duplicate, no error for a legitimate retry |
| Commit succeeded but the acknowledgement was lost | App-generated UUID v7; on an unknown outcome, check whether the row exists before retrying or failing | Without it, the retried INSERT collides with its own row and reports a false `409` (EF Core's documented commit-failure problem) |
| Concurrent cancels | One atomic conditional update: `UPDATE … SET status = 'Cancelled' WHERE id = @id AND status = 'Confirmed' AND start_utc > @now`; 0 rows → load to decide `200` / `404` / `409` | Race-free without a row version; repeated cancels are idempotent |
| Cross-dealership assignment by a bug | Composite foreign keys `(technician_id, dealership_id)`, `(service_bay_id, dealership_id)` | The database guards tenant boundaries, as it does overlap |
| Transaction scope | One short transaction per insert; no I/O to other systems inside it | Minimises lock time and contention |
| Isolation level | PostgreSQL default `READ COMMITTED` | Sufficient because the constraint, not the read, guarantees correctness |

---

## 9. API contract

Full contract: OpenAPI document generated by the API (`/openapi/v1.json`).

| Method | Route | Success | Errors |
|---|---|---|---|
| `POST` | `/api/appointments` | `201` + `Location` | `400`, `404`, `409`, `422` |
| `GET` | `/api/appointments/{id}` | `200` | `404` |
| `POST` | `/api/appointments/{id}/cancel` | `200` (also when already cancelled) | `404`, `409` |
| `GET` | `/api/dealerships/{id}/availability?serviceTypeId=&date=` | `200` | `400`, `404`, `422` |
| `GET` | `/health/live`, `/health/ready` | `200` | `503` |

**Request**

```http
POST /api/appointments
Idempotency-Key: 7d0f6a52-5c1e-4f0e-9a2b-3f4c2d1e0b9a
Content-Type: application/json

{
  "customerId": "c1a2...",
  "vehicleId": "v9b8...",
  "serviceTypeId": "s-oil-change",
  "dealershipId": "d-riverside",
  "requestedStart": "2026-11-03T10:00:00+00:00"
}
```

**201 Created**

```json
{
  "id": "a3f1...",
  "status": "Confirmed",
  "startUtc": "2026-11-03T10:00:00Z",
  "endUtc": "2026-11-03T11:00:00Z",
  "localStart": "2026-11-03T10:00:00+00:00",
  "technician": { "id": "t-alice", "name": "Alice" },
  "serviceBay": { "id": "b-1", "name": "Bay-1", "type": "Standard" }
}
```

**409 Conflict** (RFC 9457)

```json
{
  "type": "https://errors.service-scheduler/no-qualified-technician-available",
  "title": "No qualified technician is available for the whole service duration.",
  "status": 409,
  "code": "NoQualifiedTechnicianAvailable",
  "alternatives": ["2026-11-03T11:00:00Z", "2026-11-03T16:15:00Z", "2026-11-04T08:00:00Z"],
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

`alternatives` holds up to 3 start times within 7 days and may be empty.

| Error | HTTP | Meaning for the advisor |
|---|---|---|
| Shape errors (missing field, no offset on `requestedStart`, bad `Idempotency-Key`) | 400 | Fix the request |
| `NotFound` | 404 | Wrong ID |
| `VehicleNotOwnedByCustomer`, `ServiceNotOfferedAtDealership`, `StartInPast`, `BeyondBookingHorizon`, `StartNotOnGrid`, `OutsideOpeningHours`, `IdempotencyKeyReused` | 422 | This request can never succeed as sent |
| `NoQualifiedTechnicianAvailable`, `NoCompatibleBayAvailable`, `VehicleAlreadyBooked`, `AppointmentAlreadyStarted` | 409 | Conflicts with the current state; pick another time |

---

## 10. Technology choices

| Concern | Choice | Justification | Alternatives considered |
|---|---|---|---|
| Runtime | **.NET 10 (LTS)** | Long-term support, high performance, built-in OpenAPI, validation, `TimeProvider`, ProblemDetails | .NET 8 (older LTS); Node/Java (less aligned with the author's experience) |
| API style | **ASP.NET Core minimal APIs** | Low ceremony, `TypedResults` give accurate OpenAPI, route groups for cross-cutting policies | MVC controllers (more boilerplate for a small surface) |
| Database | **PostgreSQL 16** | Exclusion constraints + `tstzrange` express "no overlap" natively; strong concurrency semantics; managed options on every cloud | SQL Server (no exclusion constraints; would need serializable transactions or locking); document stores (no cross-document constraints) |
| Data access | **EF Core 10 + Npgsql** | Migrations, LINQ projections, `ExecuteUpdateAsync` for atomic conditional updates, first-class PostgreSQL support | Dapper (more control but hand-written mapping and migrations) |
| Architecture | **Clean Architecture + DDD tactical patterns** | Business rules isolated and unit-testable; infrastructure replaceable | Single-project vertical slices (faster to start, weaker boundaries for this rule-heavy domain) |
| Testing | **xUnit + Testcontainers (PostgreSQL)** | Integration tests exercise the real constraints and concurrency behaviour | EF InMemory/SQLite (cannot reproduce exclusion constraints, so tests would prove nothing) |
| Observability | **OpenTelemetry + .NET Aspire Dashboard** | Vendor-neutral; same instrumentation exports to Azure Monitor, Grafana or Datadog | Vendor SDKs (lock-in) |
| Packaging | **Docker Compose** | One command for reviewers: API + database + dashboard | Local installs (fragile across machines) |

---

## 11. Observability

### Logs

- Structured JSON (message templates; `[LoggerMessage]` source generator on hot paths).
- Business events at `Information`: `AppointmentBooked`, `BookingRejected {Reason}`, `AppointmentCancelled`.
- Allocation retry after a constraint violation at `Warning` (signals contention).
- Personal data never logged; IDs and VINs only. Every entry carries `TraceId`/`SpanId`.

### Traces

| Span | Source | Key attributes |
|---|---|---|
| `POST /api/appointments` | ASP.NET Core instrumentation | `http.response.status_code` |
| `booking.allocate` | `ActivitySource("ServiceScheduler")` | `attempt`, `outcome` |
| `booking.persist` | `ActivitySource("ServiceScheduler")` | `outcome` (`added`, `conflict:technician`, …) |
| SQL statements | Npgsql instrumentation | `db.statement` (parameterised) |

### Metrics

| Metric | Type | Tags | Question it answers |
|---|---|---|---|
| `scheduler.bookings.created` | Counter | `dealership_id`, `service_type` | How much work is being booked? |
| `scheduler.bookings.rejected` | Counter | `reason` | Are we short of technicians, bays, or opening hours? |
| `scheduler.booking.allocation_retries` | Counter | `resource` | How much real contention exists? |
| `scheduler.booking.duration` | Histogram (ms) | `outcome` | Is booking latency within target? |
| HTTP and runtime metrics | Built-in | Standard | Error rate, throughput, GC, thread pool |

A rising `rejected{reason="no_technician"}` is a staffing signal for workshop managers, not just an
engineering metric.

### Health and alerting

- `/health/live` (process) and `/health/ready` (database reachable) for orchestrator probes.
- Suggested alerts: p95 booking latency > 500 ms; 5xx rate > 1 %; readiness failing; sustained
  allocation retries (contention or a stale availability view).

---

## 12. Scalability, performance and reliability

| Quality | Approach |
|---|---|
| **Horizontal scale** | Stateless API instances; no in-memory locks or sticky sessions; correctness lives in the database |
| **Query efficiency** | Bounded queries (one dealership-day), projections with `AsNoTracking`, no queries in loops (N+1), GiST indexes for overlap |
| **Connection management** | Npgsql connection pooling; one `DbContext` per request; short transactions |
| **Latency budget** | Booking ≈ 3–4 round trips (idempotency lookup, context, busy intervals, insert); target p95 < 200 ms locally |
| **Contention** | Hot spots are a single dealership-day; conflicts are resolved by retry on the next candidate, not by global locks |
| **Reliability** | Transient database errors retried via the EF Core execution strategy; timeouts on all I/O; graceful shutdown drains requests |
| **Data growth** | Appointments grow linearly per dealership; partition by `start_utc` (monthly) or archive past appointments when needed |
| **Multi-tenancy** | Every row is scoped by `dealership_id`; an authorisation policy can restrict advisors to their dealerships in one place |
| **Maintainability** | Clean Architecture boundaries, ADRs for decisions, an edge-case catalogue mapped to tests |

---

## 13. How GenAI was used in the design phase

Source: [`ai-log.md`](ai-log.md). Summary as of this draft:

| Step | AI's role | Developer's role |
|---|---|---|
| Scenario selection | Broke down all four scenarios: business need, hardest problem, edge cases | Cross-checked against a second AI's analysis, challenged both, chose A against the brief's grading criteria |
| Guardrails | Generated the repository skeleton, `CLAUDE.md`, path-scoped rules, commands, agents and hooks | Rejected the first `CLAUDE.md` as too thin; specified the standards to cover; verified the build and pipe-tested the hooks |
| Design document | Drafted the C4 diagrams, class/ER/sequence diagrams and this document from the agreed decisions | Reviewing every diagram against the business rules; owning the assumptions in ADR-0002 |
| Edge-case review (v0.1 → v0.2) | Asked to audit its own draft, found 6 defects: an impossible DST test case, technician allocation contradicting bay allocation, a false `409` after a lost commit acknowledgement, a vehicle check blind to other dealerships, no guard against cross-dealership assignment, an unreachable `Completed` status. Proposed ~30 missing edge cases | Decided the 4 business questions it could not (technician best fit, buffer past closing, 7-day alternatives, `ServiceNotOfferedAtDealership`) |

_To be completed: further corrections made during the developer's own review._

---

## 14. Future work

| Item | How the design accommodates it |
|---|---|
| Authentication and per-dealership authorisation | Route groups allow one policy; every row already carries `dealership_id` |
| Notifications | Add an outbox table written in the booking transaction; a worker publishes `AppointmentBooked` |
| DMS integration | Reference-data sync job; the domain already references customers and vehicles by ID |
| Technician leave, lunch breaks, bay maintenance, public holidays | Add `unavailability` intervals; they join the busy intervals with no allocator change |
| Workshop lifecycle (`InProgress`, `Completed`, `NoShow`) | New status values and transitions on `Appointment`. The exclusion constraints count only `Confirmed` rows, so any new state that occupies resources (e.g. `InProgress`) must be added to their `WHERE` clause |
| Rescheduling | Cancel + book in one transaction; constraints still apply |
| Multi-technician jobs | Allocation returns a set of technicians; a join table replaces the single foreign key |
| Customer self-service | New client container over the same API, with rate limiting |
