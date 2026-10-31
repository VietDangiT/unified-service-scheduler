using ServiceScheduler.Application.Appointments;

namespace ServiceScheduler.Api.Appointments;

public sealed record AppointmentResponse(
    Guid Id,
    string Status,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid DealershipId,
    DateTime Start,
    DateTime End,
    AppointmentTechnicianResponse Technician,
    AppointmentServiceBayResponse ServiceBay)
{
    public static AppointmentResponse From(AppointmentDto appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        return new AppointmentResponse(
            appointment.Id,
            appointment.Status,
            appointment.CustomerId,
            appointment.VehicleId,
            appointment.ServiceTypeId,
            appointment.DealershipId,
            appointment.Start,
            appointment.End,
            new AppointmentTechnicianResponse(appointment.Technician.Id, appointment.Technician.Name),
            new AppointmentServiceBayResponse(appointment.ServiceBay.Id, appointment.ServiceBay.Name, appointment.ServiceBay.Type));
    }
}

public sealed record AppointmentTechnicianResponse(Guid Id, string Name);

public sealed record AppointmentServiceBayResponse(Guid Id, string Name, string Type);
