using ServiceScheduler.Domain.Common;

namespace ServiceScheduler.Domain.Scheduling;

public static class BookingErrors
{
    public static DomainError NotFound { get; } = new(
        "NotFound", "The customer, vehicle, service type or dealership does not exist.", ErrorKind.NotFound);

    public static DomainError VehicleNotOwnedByCustomer { get; } = new(
        "VehicleNotOwnedByCustomer", "The vehicle does not belong to the customer.", ErrorKind.Validation);

    public static DomainError ServiceNotOfferedAtDealership { get; } = new(
        "ServiceNotOfferedAtDealership",
        "The dealership has no active compatible service bay or no active technician with the required skill.",
        ErrorKind.Validation);

    public static DomainError StartInPast { get; } = new(
        "StartInPast", "The requested start time must be in the future.", ErrorKind.Validation);

    public static DomainError StartNotOnGrid { get; } = new(
        "StartNotOnGrid", "The requested start time must be on a 15-minute boundary.", ErrorKind.Validation);

    public static DomainError OutsideOpeningHours { get; } = new(
        "OutsideOpeningHours", "The service must start and finish within the dealership's opening hours.", ErrorKind.Validation);

    public static DomainError NoQualifiedTechnicianAvailable { get; } = new(
        "NoQualifiedTechnicianAvailable", "No qualified technician is free for the whole service.", ErrorKind.Conflict);

    public static DomainError NoCompatibleBayAvailable { get; } = new(
        "NoCompatibleBayAvailable", "No compatible service bay is free for the whole service.", ErrorKind.Conflict);
}
