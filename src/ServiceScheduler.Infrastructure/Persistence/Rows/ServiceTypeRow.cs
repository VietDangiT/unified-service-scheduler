using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class ServiceTypeRow
{
    public Guid Id { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }

    public int DurationMinutes { get; init; }

    public int BufferMinutes { get; init; }

    public required string RequiredSkill { get; init; }

    public BayType RequiredBayType { get; init; }
}
