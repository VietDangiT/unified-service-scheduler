using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.ServiceTypes;

/// <summary>What a booking asks for: how long the service takes and which skill and bay type it needs.</summary>
public sealed record ServiceType(ServiceTypeId Id, TimeSpan Duration, Skill RequiredSkill, BayType RequiredBayType);

public readonly record struct ServiceTypeId(Guid Value);
