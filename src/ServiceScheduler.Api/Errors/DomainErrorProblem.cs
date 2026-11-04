using System.Text.Json;

using Microsoft.AspNetCore.Http.HttpResults;

using ServiceScheduler.Domain.Common;

namespace ServiceScheduler.Api.Errors;

/// <summary>The one place that turns an expected failure into an RFC 9457 response.</summary>
internal static class DomainErrorProblem
{
    private const string TypeBaseUri = "https://errors.service-scheduler/";

    public static ProblemHttpResult ToProblem(this DomainError error) =>
        TypedResults.Problem(
            title: error.Message,
            statusCode: StatusCodeOf(error.Kind),
            type: TypeBaseUri + JsonNamingPolicy.KebabCaseLower.ConvertName(error.Code),
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    private static int StatusCodeOf(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "This error kind has no HTTP status."),
    };
}
