using ServiceScheduler.Domain.Common;

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

        var zone = request.Dealership.TimeZone;

        if (request.Vehicle.OwnerId != request.CustomerId)
        {
            return Result.Failure<TimeSlot>(BookingErrors.VehicleNotOwnedByCustomer);
        }

        if (request.RequestedStart <= now)
        {
            return Result.Failure<TimeSlot>(BookingErrors.StartInPast);
        }

        if (!IsOnGrid(request.RequestedStart, zone))
        {
            return Result.Failure<TimeSlot>(BookingErrors.StartNotOnGrid);
        }

        var slot = TimeSlot.Create(request.RequestedStart, request.ServiceDuration);

        if (!request.Dealership.OpeningHours.Covers(slot, zone))
        {
            return Result.Failure<TimeSlot>(BookingErrors.OutsideOpeningHours);
        }

        return Result.Success(slot);
    }

    // Checked on the local wall clock; whole ticks, so seconds and milliseconds also fail.
    private static bool IsOnGrid(DateTimeOffset start, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(start, zone).TimeOfDay.Ticks % SlotGranularity.Ticks == 0;
}
