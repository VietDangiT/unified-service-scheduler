namespace ServiceScheduler.Domain.Dealerships;

/// <summary>A capability a technician holds and a service type requires; matched exactly by <see cref="Code"/>.</summary>
public sealed record Skill(string Code);
