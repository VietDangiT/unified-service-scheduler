using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.Application.Appointments;

/// <summary>
/// The reference data a booking needs. Names are only shown to the advisor and no rule reads them, so they
/// travel beside the domain objects.
/// </summary>
public sealed record BookingContext(
    Vehicle Vehicle,
    ServiceType ServiceType,
    Dealership Dealership,
    IReadOnlyDictionary<TechnicianId, string> TechnicianNames,
    IReadOnlyDictionary<ServiceBayId, string> BayNames)
{
    /// <summary>Shown when no code was given; the reader always gives one.</summary>
    public const string UnknownServiceTypeCode = "unknown";

    /// <summary>
    /// The service type's stable code, which tags the bookings metric. No rule reads it, so it stays out of the domain
    /// <see cref="ServiceType"/>.
    /// </summary>
    public string ServiceTypeCode { get; init; } = UnknownServiceTypeCode;
}
