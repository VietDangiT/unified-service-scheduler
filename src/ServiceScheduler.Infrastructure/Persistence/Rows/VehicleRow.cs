namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class VehicleRow
{
    public Guid Id { get; init; }

    public Guid CustomerId { get; init; }

    public required string Vin { get; init; }

    public required string Make { get; init; }

    public required string Model { get; init; }

    public short ModelYear { get; init; }
}
