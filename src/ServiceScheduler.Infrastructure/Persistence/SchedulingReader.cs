using Microsoft.EntityFrameworkCore;

using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Domain.ServiceTypes;
using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence;

public sealed class SchedulingReader(SchedulingDbContext db) : ISchedulingReader
{
    public async Task<BookingContext?> GetBookingContextAsync(BookAppointmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool customerExists = await db.Customers.AnyAsync(customer => customer.Id == command.CustomerId, cancellationToken);
        VehicleRow? vehicle = await db.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == command.VehicleId, cancellationToken);
        ServiceTypeRow? serviceType = await db.ServiceTypes.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == command.ServiceTypeId, cancellationToken);
        DealershipRow? dealership = await LoadDealershipAsync(command.DealershipId, cancellationToken);

        if (!customerExists || vehicle is null || serviceType is null || dealership is null)
        {
            return null;
        }

        return new BookingContext(
            ToVehicle(vehicle),
            ToServiceType(serviceType),
            ToDealership(dealership),
            dealership.Technicians.ToDictionary(row => new TechnicianId(row.Id), row => row.Name),
            dealership.ServiceBays.ToDictionary(row => new ServiceBayId(row.Id), row => row.Name));
    }

    public async Task<BusyIntervals> GetBusyIntervalsAsync(DealershipId dealershipId, TimeSlot window, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(window);

        var appointments = await db.Appointments.AsNoTracking()
            .Where(appointment => appointment.DealershipId == dealershipId.Value
                && appointment.Status == AppointmentStatus.Confirmed
                && appointment.StartAt < window.End
                && window.Start < appointment.BlockedUntil)
            .Select(appointment => new { appointment.TechnicianId, appointment.ServiceBayId, appointment.StartAt, appointment.BlockedUntil })
            .ToListAsync(cancellationToken);

        return new BusyIntervals(
            [.. appointments.Select(appointment => new TechnicianBusyInterval(
                new TechnicianId(appointment.TechnicianId),
                TimeSlot.Create(appointment.StartAt, appointment.BlockedUntil - appointment.StartAt)))],
            [.. appointments.Select(appointment => new BayBusyInterval(
                new ServiceBayId(appointment.ServiceBayId),
                TimeSlot.Create(appointment.StartAt, appointment.BlockedUntil - appointment.StartAt)))]);
    }

    private Task<DealershipRow?> LoadDealershipAsync(Guid dealershipId, CancellationToken cancellationToken) =>
        db.Dealerships.AsNoTracking()
            .AsSplitQuery()
            .Include(dealership => dealership.OpeningHours)
            .Include(dealership => dealership.ServiceBays)
            .Include(dealership => dealership.Technicians).ThenInclude(technician => technician.Skills)
            .Include(dealership => dealership.Technicians).ThenInclude(technician => technician.Shifts)
            .SingleOrDefaultAsync(dealership => dealership.Id == dealershipId, cancellationToken);

    private static Vehicle ToVehicle(VehicleRow row) =>
        new(new VehicleId(row.Id), new CustomerId(row.CustomerId));

    private static ServiceType ToServiceType(ServiceTypeRow row) =>
        new(new ServiceTypeId(row.Id), TimeSpan.FromMinutes(row.DurationMinutes), new Skill(row.RequiredSkill), row.RequiredBayType);

    private static Dealership ToDealership(DealershipRow row)
    {
        var id = new DealershipId(row.Id);
        var openingHours = row.OpeningHours.ToDictionary(
            hours => (DayOfWeek)hours.DayOfWeek,
            hours => new TimeRange(hours.OpensAt, hours.ClosesAt));

        return new Dealership(
            id,
            new WeeklySchedule(openingHours),
            row.Technicians.Select(technician => ToTechnician(technician, id)),
            row.ServiceBays.Select(bay => new ServiceBay(new ServiceBayId(bay.Id), id, bay.BayType, bay.IsActive)));
    }

    private static Technician ToTechnician(TechnicianRow row, DealershipId dealershipId)
    {
        var shifts = row.Shifts.ToDictionary(
            shift => (DayOfWeek)shift.DayOfWeek,
            shift => new TimeRange(shift.StartsAt, shift.EndsAt));

        return new Technician(
            new TechnicianId(row.Id),
            dealershipId,
            row.Skills.Select(skill => new Skill(skill.Skill)),
            new WeeklySchedule(shifts),
            row.IsActive);
    }
}
