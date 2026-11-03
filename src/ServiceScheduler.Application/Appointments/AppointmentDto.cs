namespace ServiceScheduler.Application.Appointments;

public sealed record AppointmentDto(
    Guid Id,
    string Status,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid DealershipId,
    DateTime Start,
    DateTime End,
    AppointmentTechnicianDto Technician,
    AppointmentServiceBayDto ServiceBay);

public sealed record AppointmentTechnicianDto(Guid Id, string Name);

public sealed record AppointmentServiceBayDto(Guid Id, string Name, string Type);
