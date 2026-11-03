namespace ServiceScheduler.Api.Appointments;

/// <summary>
/// The booking events the API writes. The event names and value names are a contract for whoever reads the logs.
/// Only IDs and error codes go in: never the customer's name, email or phone.
/// </summary>
internal static partial class BookingLog
{
    [LoggerMessage(
        EventId = 1000,
        EventName = "AppointmentBooked",
        Level = LogLevel.Information,
        Message = "Appointment {AppointmentId} booked at dealership {DealershipId} with technician {TechnicianId} in service bay {ServiceBayId}")]
    public static partial void AppointmentBooked(
        this ILogger logger, Guid appointmentId, Guid dealershipId, Guid technicianId, Guid serviceBayId);

    // Expected rejections are business outcomes, not failures needing attention, hence Information.
    [LoggerMessage(
        EventId = 1001,
        EventName = "BookingRejected",
        Level = LogLevel.Information,
        Message = "Booking rejected at dealership {DealershipId}: {Reason}")]
    public static partial void BookingRejected(this ILogger logger, Guid dealershipId, string reason);
}
