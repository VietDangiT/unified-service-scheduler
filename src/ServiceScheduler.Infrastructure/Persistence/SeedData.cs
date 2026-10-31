namespace ServiceScheduler.Infrastructure.Persistence;

/// <summary>
/// Fixed IDs of the reference data the first migration seeds (ADR-0002 #22). The first group of each ID hints
/// at what it identifies, so a row or a log line can be read at a glance.
/// </summary>
public static class SeedData
{
    public static readonly Guid DealershipId = new("dea10000-0000-0000-0000-000000000001");
    public static readonly Guid TechnicianId = new("7ec40000-0000-0000-0000-000000000001");
    public static readonly Guid ServiceBayId = new("ba900000-0000-0000-0000-000000000001");
    public static readonly Guid CustomerId = new("c0570000-0000-0000-0000-000000000001");
    public static readonly Guid VehicleId = new("ca700000-0000-0000-0000-000000000001");
    public static readonly Guid OilChangeServiceTypeId = new("5e7c0000-0000-0000-0000-000000000001");

    internal const string GeneralSkill = "General";
}
