using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence;

/// <summary>
/// The demo dealership Northgate, seeded by the <c>NorthgateDemoSeed</c> migration for the walkthrough in
/// <c>ServiceScheduler.Api.http</c>. IDs keep the first group of <see cref="SeedData"/> (what the row is) and use
/// <c>0002</c> as the second group (the Northgate set), so they stay fixed and readable.
/// </summary>
internal static class NorthgateDemoData
{
    public static readonly Guid DealershipId = new("dea10000-0002-0000-0000-000000000001");

    public static readonly Guid BenId = new("7ec40000-0002-0000-0000-000000000001");
    public static readonly Guid ChloeId = new("7ec40000-0002-0000-0000-000000000002");
    public static readonly Guid DanId = new("7ec40000-0002-0000-0000-000000000003");

    public static readonly Guid BayAId = new("ba900000-0002-0000-0000-000000000001");
    public static readonly Guid BayBId = new("ba900000-0002-0000-0000-000000000002");
    public static readonly Guid BayEvId = new("ba900000-0002-0000-0000-000000000003");
    public static readonly Guid BayAlignId = new("ba900000-0002-0000-0000-000000000004");

    public static readonly Guid EvBatteryCheckServiceTypeId = new("5e7c0000-0002-0000-0000-000000000001");
    public static readonly Guid WheelAlignmentServiceTypeId = new("5e7c0000-0002-0000-0000-000000000002");

    public static readonly Guid EmmaId = new("c0570000-0002-0000-0000-000000000001");
    public static readonly Guid LiamId = new("c0570000-0002-0000-0000-000000000002");
    public static readonly Guid SofiaId = new("c0570000-0002-0000-0000-000000000003");

    public static readonly Guid FocusId = new("ca700000-0002-0000-0000-000000000001");
    public static readonly Guid CorollaId = new("ca700000-0002-0000-0000-000000000002");
    public static readonly Guid GolfId = new("ca700000-0002-0000-0000-000000000003");
    public static readonly Guid LeafId = new("ca700000-0002-0000-0000-000000000004");

    private const string EvSkill = "EV";
    private const string AlignmentSkill = "Alignment";

    private static readonly TimeOnly Opens = new(8, 0);
    private static readonly TimeOnly Closes = new(17, 0);
    private static readonly TimeOnly DanStarts = new(12, 0);

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public static DealershipRow Dealership { get; } = new() { Id = DealershipId, Name = "Northgate" };

    public static IReadOnlyList<OpeningHoursRow> OpeningHours { get; } =
    [
        .. Weekdays.Select(day => new OpeningHoursRow
        {
            DealershipId = DealershipId,
            DayOfWeek = (short)day,
            OpensAt = Opens,
            ClosesAt = Closes,
        }),
    ];

    public static IReadOnlyList<TechnicianRow> Technicians { get; } =
    [
        Technician(BenId, "Ben"),
        Technician(ChloeId, "Chloe"),
        Technician(DanId, "Dan"),
    ];

    public static IReadOnlyList<TechnicianSkillRow> TechnicianSkills { get; } =
    [
        new() { TechnicianId = BenId, Skill = SeedData.GeneralSkill },
        new() { TechnicianId = ChloeId, Skill = SeedData.GeneralSkill },
        new() { TechnicianId = ChloeId, Skill = EvSkill },
        new() { TechnicianId = DanId, Skill = SeedData.GeneralSkill },
        new() { TechnicianId = DanId, Skill = AlignmentSkill },
    ];

    public static IReadOnlyList<TechnicianShiftRow> TechnicianShifts { get; } =
    [
        .. WeekdayShifts(BenId, Opens, Closes),
        .. WeekdayShifts(ChloeId, Opens, Closes),
        .. WeekdayShifts(DanId, DanStarts, Closes),
    ];

    public static IReadOnlyList<ServiceBayRow> ServiceBays { get; } =
    [
        Bay(BayAId, "Bay-A", BayType.Standard),
        Bay(BayBId, "Bay-B", BayType.Standard),
        Bay(BayEvId, "Bay-EV", BayType.EV),
        Bay(BayAlignId, "Bay-Align", BayType.Alignment),
    ];

    public static IReadOnlyList<ServiceTypeRow> ServiceTypes { get; } =
    [
        new()
        {
            Id = EvBatteryCheckServiceTypeId,
            Code = "ev-battery-check",
            Name = "EV battery check",
            DurationMinutes = 90,
            BufferMinutes = 0,
            RequiredSkill = EvSkill,
            RequiredBayType = BayType.EV,
        },
        new()
        {
            Id = WheelAlignmentServiceTypeId,
            Code = "wheel-alignment",
            Name = "Wheel alignment",
            DurationMinutes = 60,
            BufferMinutes = 0,
            RequiredSkill = AlignmentSkill,
            RequiredBayType = BayType.Alignment,
        },
    ];

    // Fictitious contact details: example.com addresses and the Ofcom drama range (020 7946 0xxx).
    public static IReadOnlyList<CustomerRow> Customers { get; } =
    [
        new() { Id = EmmaId, FullName = "Emma Clarke", Email = "emma.clarke@example.com", Phone = "+44 20 7946 0101" },
        new() { Id = LiamId, FullName = "Liam Patel", Email = "liam.patel@example.com", Phone = "+44 20 7946 0102" },
        new() { Id = SofiaId, FullName = "Sofia Rossi", Email = "sofia.rossi@example.com", Phone = "+44 20 7946 0103" },
    ];

    public static IReadOnlyList<VehicleRow> Vehicles { get; } =
    [
        new() { Id = FocusId, CustomerId = EmmaId, Vin = "WF0NXXGCHNLA12345", Make = "Ford", Model = "Focus", ModelYear = 2020 },
        new() { Id = CorollaId, CustomerId = LiamId, Vin = "SB1K93BE10E123456", Make = "Toyota", Model = "Corolla", ModelYear = 2022 },
        new() { Id = GolfId, CustomerId = SofiaId, Vin = "WVWZZZAUZKW123456", Make = "Volkswagen", Model = "Golf", ModelYear = 2019 },
        new() { Id = LeafId, CustomerId = SofiaId, Vin = "SJNFAAZE1U0123456", Make = "Nissan", Model = "Leaf", ModelYear = 2023 },
    ];

    private static TechnicianRow Technician(Guid id, string name) =>
        new() { Id = id, DealershipId = DealershipId, Name = name, IsActive = true };

    private static ServiceBayRow Bay(Guid id, string name, BayType bayType) =>
        new() { Id = id, DealershipId = DealershipId, Name = name, BayType = bayType, IsActive = true };

    private static IEnumerable<TechnicianShiftRow> WeekdayShifts(Guid technicianId, TimeOnly startsAt, TimeOnly endsAt) =>
        Weekdays.Select(day => new TechnicianShiftRow
        {
            TechnicianId = technicianId,
            DayOfWeek = (short)day,
            StartsAt = startsAt,
            EndsAt = endsAt,
        });
}
