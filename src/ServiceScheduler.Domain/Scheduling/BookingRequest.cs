using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>The facts needed to validate a booking, already loaded by the application layer.</summary>
public sealed record BookingRequest(
    CustomerId CustomerId,
    Vehicle Vehicle,
    Dealership Dealership,
    DateTime RequestedStart,
    ServiceType ServiceType);
