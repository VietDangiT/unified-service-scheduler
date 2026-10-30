namespace ServiceScheduler.Domain.Dealerships;

public sealed record Dealership(DealershipId Id, TimeZoneInfo TimeZone, WeeklySchedule OpeningHours);
