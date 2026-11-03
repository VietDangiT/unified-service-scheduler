using Microsoft.AspNetCore.Diagnostics;

using ServiceScheduler.Api.Appointments;
using ServiceScheduler.Api.Errors;
using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

string connectionString = builder.Configuration.GetConnectionString("Scheduling")
    ?? throw new InvalidOperationException("The connection string 'ConnectionStrings:Scheduling' is not configured.");

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.Configure<ExceptionHandlerOptions>(options =>
    // The handler logs the exception itself; the middleware must not log it a second time.
    options.SuppressDiagnosticsCallback = context => context.ExceptionHandledBy == ExceptionHandledType.ExceptionHandlerService);

// A malformed body is the client's mistake: a 400 in every environment, never an exception for the global handler.
// Development would otherwise throw BadHttpRequestException, which the handler would turn into a 500.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBayAllocationPolicy, BestFitBayPolicy>();
builder.Services.AddSingleton<ITechnicianAllocationPolicy, BestFitTechnicianPolicy>();
builder.Services.AddSingleton<SlotAllocator>();
builder.Services.AddScoped<BookAppointmentHandler>();
builder.Services.AddSchedulingPersistence(connectionString);

WebApplication app = builder.Build();

// In every environment, so the Development exception page never shows exception details to a client.
app.UseExceptionHandler();

// An error response without a body (a binding failure's 400) still gets a ProblemDetails body with a traceId.
app.UseStatusCodePages();

// The contract that represents the client layer (system design section 9), served at /openapi/v1.json.
app.MapOpenApi();

app.MapGroup("/api").MapAppointmentEndpoints();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    // The host is not started yet, so no stopping token exists; a killed migration rolls back (PostgreSQL DDL is transactional).
    await app.Services.MigrateSchedulingDatabaseAsync(CancellationToken.None);
}

await app.RunAsync();
