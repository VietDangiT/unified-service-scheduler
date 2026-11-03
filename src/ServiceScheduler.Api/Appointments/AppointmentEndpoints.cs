using Microsoft.AspNetCore.Http.HttpResults;

using ServiceScheduler.Api.Errors;
using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Common;

namespace ServiceScheduler.Api.Appointments;

internal static class AppointmentEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/appointments", BookAsync)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return routes;
    }

    private static async Task<Results<Created<AppointmentResponse>, ProblemHttpResult>> BookAsync(
        BookAppointmentRequest request,
        BookAppointmentHandler handler,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger(typeof(AppointmentEndpoints));

        Result<AppointmentDto> result = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        if (!result.IsSuccess)
        {
            logger.BookingRejected(request.DealershipId, result.Error.Code);
            return result.Error.ToProblem();
        }

        AppointmentDto appointment = result.Value;
        logger.AppointmentBooked(appointment.Id, appointment.DealershipId, appointment.Technician.Id, appointment.ServiceBay.Id);

        AppointmentResponse response = AppointmentResponse.From(appointment);

        return TypedResults.Created($"/api/appointments/{response.Id}", response);
    }
}
