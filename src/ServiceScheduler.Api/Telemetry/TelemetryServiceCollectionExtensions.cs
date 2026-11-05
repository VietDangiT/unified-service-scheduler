using Npgsql;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using ServiceScheduler.Application.Appointments;

namespace ServiceScheduler.Api.Telemetry;

internal static class TelemetryServiceCollectionExtensions
{
    private const string ServiceName = "service-scheduler";

    // The standard variable the OTLP exporter reads itself; it is also the switch for exporting at all.
    private const string OtlpEndpointSetting = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// Traces (HTTP, the booking's own spans, SQL), metrics (the booking meter and ASP.NET Core) and logs, sent over OTLP
    /// only when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set, so tests and local runs need no collector.
    /// </summary>
    public static IServiceCollection AddSchedulerTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        OpenTelemetryBuilder telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddSource(BookingTelemetry.Name)
                .AddAspNetCoreInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddMeter(BookingTelemetry.Name)
                .AddAspNetCoreInstrumentation())
            .WithLogging(configureBuilder: null, configureOptions: options =>
            {
                // Scopes carry the request's context; the formatted message is what a reader of the dashboard looks for.
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
            });

        if (!string.IsNullOrWhiteSpace(configuration[OtlpEndpointSetting]))
        {
            telemetry.UseOtlpExporter();
        }

        return services;
    }
}
