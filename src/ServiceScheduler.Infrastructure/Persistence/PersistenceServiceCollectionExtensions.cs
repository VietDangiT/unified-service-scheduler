using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ServiceScheduler.Application.Appointments;

namespace ServiceScheduler.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulingPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SchedulingDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<ISchedulingReader, SchedulingReader>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();

        return services;
    }
}
