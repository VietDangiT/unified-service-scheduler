using System.Text.Json.Serialization;

using ServiceScheduler.Application.Appointments;

namespace ServiceScheduler.Api.Appointments;

// A property the contract does not define (a typo such as "requestStart") is a malformed body, never silently ignored;
// on this type only, so response and ProblemDetails schemas in the OpenAPI document stay open.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
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
