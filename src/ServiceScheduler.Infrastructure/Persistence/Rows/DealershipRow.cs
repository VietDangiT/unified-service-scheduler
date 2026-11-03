namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class DealershipRow
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public ICollection<OpeningHoursRow> OpeningHours { get; } = [];

    public ICollection<TechnicianRow> Technicians { get; } = [];

    public ICollection<ServiceBayRow> ServiceBays { get; } = [];
}
