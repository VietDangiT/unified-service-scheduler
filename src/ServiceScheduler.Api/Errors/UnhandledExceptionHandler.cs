using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ServiceScheduler.Api.Errors;

/// <summary>
/// The one place an unexpected exception becomes a response: logged once here, and the client gets a generic 500
/// problem with nothing of the exception in it (no message, type or stack trace).
/// </summary>
internal sealed partial class UnhandledExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    private const string Title = "An unexpected error occurred.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // The exception is deliberately not passed to the context, so no writer can add its details to the body.
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = Title },
        });

        // Handled even when no writer could produce a body: the 500 stands and the exception is already logged.
        return true;
    }

    [LoggerMessage(
        EventId = 5000,
        EventName = "UnhandledRequestException",
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {RequestMethod} {RequestPath}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string requestMethod, string requestPath);
}
