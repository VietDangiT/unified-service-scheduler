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
        CancellationToken cancellationToken)
    {
        Result<AppointmentDto> result = await handler.HandleAsync(request.ToCommand(), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.ToProblem();
        }

        AppointmentResponse response = AppointmentResponse.From(result.Value);

        return TypedResults.Created($"/api/appointments/{response.Id}", response);
    }
}
