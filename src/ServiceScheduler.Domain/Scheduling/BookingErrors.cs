using ServiceScheduler.Domain.Common;

namespace ServiceScheduler.Domain.Scheduling;

public static class BookingErrors
{
    public static DomainError VehicleNotOwnedByCustomer { get; } = new(
        "VehicleNotOwnedByCustomer", "The vehicle does not belong to the customer.", ErrorKind.Validation);

    public static DomainError StartInPast { get; } = new(
        "StartInPast", "The requested start time must be in the future.", ErrorKind.Validation);

    public static DomainError StartNotOnGrid { get; } = new(
        "StartNotOnGrid", "The requested start time must be on a 15-minute boundary.", ErrorKind.Validation);

    public static DomainError OutsideOpeningHours { get; } = new(
        "OutsideOpeningHours", "The service must start and finish within the dealership's opening hours.", ErrorKind.Validation);
}
