---
paths:
  - "**/*.cs"
---

# C# design standards

## SOLID, applied pragmatically

- **S**: one reason to change per class. A handler orchestrates; it does not also format HTTP
  responses or build SQL.
- **O**: extend behaviour by adding a type (a new allocation strategy), not by editing a growing `switch`.
- **L**: implementations of a port honour its contract fully, including cancellation and error semantics.
- **I**: small, role-specific interfaces (`IAppointmentRepository`, `IAvailabilityReader`), not a
  generic `IRepository<T>` with twenty members.
- **D**: Application defines the interfaces it needs; Infrastructure implements them.
  Interfaces exist where there is a real seam (I/O, time, multiple strategies), not for every class.

## DRY, with judgement

Remove duplicated *knowledge* (a business rule, a constant, a mapping), not merely similar-looking
code. Apply the rule of three before extracting an abstraction. A wrong abstraction costs more
than a little duplication. Test code may repeat setup for readability; use builders for shared fixtures.

## Patterns in use (and only these unless agreed)

| Pattern | Where | Why |
|---|---|---|
| Result / domain error | Domain + Application return values | Expected business failures are values, not exceptions |
| Strategy | `IBayAllocationPolicy`, `ITechnicianAllocationPolicy` | Allocation rules can vary without touching the booking flow |
| Repository + Unit of Work | Ports in Application, EF Core in Infrastructure | Aggregate persistence without leaking EF Core |
| Factory method | `Appointment.Book(...)`, value-object `Create(...)` | Objects are valid from construction |
| Options | `IOptions<BookingOptions>` | Grid size, retry count, buffers are configuration |
| Decorator (if needed) | Cross-cutting concerns around handlers | Keep handlers single-purpose |

## Language and style

- `sealed` by default for classes not designed for inheritance.
- Immutability by default: `record`/`readonly record struct` for value objects and DTOs,
  `init`-only properties, `IReadOnlyCollection<T>` for exposed collections.
- Nullable reference types are on and warnings are errors. No `!` null-forgiving operator except
  in tests or with a comment explaining why it is safe.
- Guard clauses at the top of methods; return early; keep nesting to two levels.
- Strongly typed IDs (`readonly record struct AppointmentId(Guid Value)`).
- Name things by intent: `IsAvailableFor(slot)` not `Check()`. No abbreviations, no Hungarian notation.
- No magic numbers or strings. Business constants live in the domain or in options.
- Methods short enough to read without scrolling. Extract when a comment would otherwise explain a block.
- Comments explain *why*, never *what*. Public types in Domain and Application get XML docs only when the
  name is not self-explanatory.
- `DateTime` never appears in Domain; use `DateTimeOffset` (UTC) and `TimeProvider`.
- Collections: avoid multiple enumeration of `IEnumerable<T>`; materialise once when needed.
