using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class ServiceBayRow
{
    public Guid Id { get; init; }

    public Guid DealershipId { get; init; }

    public required string Name { get; init; }

    public BayType BayType { get; init; }

    public bool IsActive { get; init; }
}
