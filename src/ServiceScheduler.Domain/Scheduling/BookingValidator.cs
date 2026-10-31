using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>
/// Request-level booking rules, checked in the documented order so the same input always yields
/// the same error (see .claude/rules/booking-domain.md).
/// </summary>
public static class BookingValidator
{
    public static readonly TimeSpan SlotGranularity = TimeSpan.FromMinutes(15);

    /// <returns>The service interval when every rule passes; otherwise the first failing rule.</returns>
    public static Result<TimeSlot> Validate(BookingRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Vehicle.OwnerId != request.CustomerId)
        {
            return Result.Failure<TimeSlot>(BookingErrors.VehicleNotOwnedByCustomer);
        }

        if (!IsOffered(request.Dealership, request.ServiceType))
        {
            return Result.Failure<TimeSlot>(BookingErrors.ServiceNotOfferedAtDealership);
        }

        if (request.RequestedStart <= now)
        {
            return Result.Failure<TimeSlot>(BookingErrors.StartInPast);
        }

        if (!IsOnGrid(request.RequestedStart, request.Dealership.TimeZone))
        {
            return Result.Failure<TimeSlot>(BookingErrors.StartNotOnGrid);
        }

        var slot = TimeSlot.Create(request.RequestedStart, request.ServiceType.Duration);

        if (!request.Dealership.IsOpenFor(slot))
        {
            return Result.Failure<TimeSlot>(BookingErrors.OutsideOpeningHours);
        }

        return Result.Success(slot);
    }

    // A static capability check: shifts and bookings are not considered (ADR-0002 #19).
    private static bool IsOffered(Dealership dealership, ServiceType serviceType) =>
        dealership.Technicians.Any(technician => technician.IsActive && technician.IsQualifiedFor(serviceType.RequiredSkill))
        && dealership.Bays.Any(bay => bay.IsActive && bay.CanServe(serviceType.RequiredBayType));

    // Checked on the local wall clock; whole ticks, so seconds and milliseconds also fail.
    private static bool IsOnGrid(DateTimeOffset start, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(start, zone).TimeOfDay.Ticks % SlotGranularity.Ticks == 0;
}
