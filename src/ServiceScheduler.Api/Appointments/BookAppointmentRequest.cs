using ServiceScheduler.Application.Appointments;

namespace ServiceScheduler.Api.Appointments;

public sealed record BookAppointmentRequest(
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid DealershipId,
    DateTime RequestedStart)
{
    public BookAppointmentCommand ToCommand() =>
        new(CustomerId, VehicleId, ServiceTypeId, DealershipId, RequestedStart);
}
