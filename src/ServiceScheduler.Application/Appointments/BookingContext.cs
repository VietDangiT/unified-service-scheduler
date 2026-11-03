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
    IReadOnlyDictionary<ServiceBayId, string> BayNames);
