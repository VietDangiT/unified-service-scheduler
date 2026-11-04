using Microsoft.EntityFrameworkCore;

using Npgsql;

using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Infrastructure.Persistence.Rows;

namespace ServiceScheduler.Infrastructure.Persistence;

public sealed class AppointmentRepository(SchedulingDbContext db) : IAppointmentRepository
{
    // Created by the InitialSchema migration; the name of the violated constraint tells which resource conflicted.
    private const string TechnicianOverlapConstraint = "ex_appointments_technician_overlap";
    private const string BayOverlapConstraint = "ex_appointments_bay_overlap";
    private const string VehicleOverlapConstraint = "ex_appointments_vehicle_overlap";

    public async Task<Result<Appointment>> AddAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        // Disposing without a commit rolls back, which is what the exclusion-violation path below relies on.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockResourcesAsync(appointment, cancellationToken);

        AppointmentRow row = ToRow(appointment);
        db.Appointments.Add(row);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } violation)
        {
            DomainError? conflictError = ToConflictError(violation.ConstraintName);
            if (conflictError is null)
            {
                throw;
            }

            // The row was never saved; stop tracking it so the context does not try to insert it again.
            db.Entry(row).State = EntityState.Detached;

            return Result.Failure<Appointment>(conflictError);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(appointment);
    }

    // Locks are taken in a fixed order (technician, then bay) so contenders for the same technician or bay queue here
    // instead of deadlocking (40P01) inside the exclusion check; each loser then sees the winner's row and gets a clean
    // 23P01. The exclusion constraints remain the final guard (ADR-0001). NO KEY UPDATE leaves foreign-key checks unblocked.
    private async Task LockResourcesAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM technicians WHERE id = {appointment.TechnicianId.Value} FOR NO KEY UPDATE",
            cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM service_bays WHERE id = {appointment.ServiceBayId.Value} FOR NO KEY UPDATE",
            cancellationToken);
    }

    // Each overlap constraint names its resource. The vehicle's is the only guard for rule 8 until the up-front check is
    // built (ADR-0002 #26), and it is never worth a retry: another technician or bay cannot free the vehicle. Any other
    // constraint is a bug and stays an exception.
    private static DomainError? ToConflictError(string? constraintName) => constraintName switch
    {
        TechnicianOverlapConstraint => BookingErrors.NoQualifiedTechnicianAvailable,
        BayOverlapConstraint => BookingErrors.NoCompatibleBayAvailable,
        VehicleOverlapConstraint => BookingErrors.VehicleAlreadyBooked,
        _ => null,
    };

    private static AppointmentRow ToRow(Appointment appointment) => new()
    {
        Id = appointment.Id.Value,
        DealershipId = appointment.DealershipId.Value,
        CustomerId = appointment.CustomerId.Value,
        VehicleId = appointment.VehicleId.Value,
        ServiceTypeId = appointment.ServiceTypeId.Value,
        TechnicianId = appointment.TechnicianId.Value,
        ServiceBayId = appointment.ServiceBayId.Value,
        StartAt = appointment.Slot.Start,
        EndAt = appointment.Slot.End,
        BlockedUntil = appointment.BlockedUntil,
        Status = appointment.Status,
        CreatedAt = appointment.CreatedAt,
    };
}
