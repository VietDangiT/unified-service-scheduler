using ServiceScheduler.Api.Appointments;
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

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBayAllocationPolicy, BestFitBayPolicy>();
builder.Services.AddSingleton<ITechnicianAllocationPolicy, BestFitTechnicianPolicy>();
builder.Services.AddSingleton<SlotAllocator>();
builder.Services.AddScoped<BookAppointmentHandler>();
builder.Services.AddSchedulingPersistence(connectionString);

WebApplication app = builder.Build();

app.MapGroup("/api").MapAppointmentEndpoints();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    // The host is not started yet, so no stopping token exists; a killed migration rolls back (PostgreSQL DDL is transactional).
    await app.Services.MigrateSchedulingDatabaseAsync(CancellationToken.None);
}

await app.RunAsync();
