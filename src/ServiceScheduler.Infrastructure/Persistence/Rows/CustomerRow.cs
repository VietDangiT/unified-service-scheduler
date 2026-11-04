namespace ServiceScheduler.Infrastructure.Persistence.Rows;

internal sealed class CustomerRow
{
    public Guid Id { get; init; }

    public required string FullName { get; init; }

    public required string Email { get; init; }

    public required string Phone { get; init; }
}
