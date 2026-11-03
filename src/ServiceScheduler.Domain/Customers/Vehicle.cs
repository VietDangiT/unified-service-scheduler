namespace ServiceScheduler.Domain.Customers;

public sealed record Vehicle(VehicleId Id, CustomerId OwnerId);

public readonly record struct VehicleId(Guid Value);

public readonly record struct CustomerId(Guid Value);
