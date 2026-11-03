namespace ServiceScheduler.Application.Appointments;

public sealed record BookAppointmentCommand(
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid DealershipId,
    DateTime RequestedStart);
