using System.Diagnostics;

using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.Application.Appointments;

public sealed class BookAppointmentHandler(
    ISchedulingReader reader,
    IAppointmentRepository repository,
    SlotAllocator allocator,
    TimeProvider clock)
{
    private static readonly TimeSpan WholeDate = TimeSpan.FromDays(1);

    public async Task<Result<AppointmentDto>> HandleAsync(BookAppointmentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        long started = clock.GetTimestamp();

        BookingContext? context = await reader.GetBookingContextAsync(command, cancellationToken);
        if (context is null)
        {
            return Rejected(BookingErrors.NotFound, started);
        }

        Result<AppointmentDto> result = await BookAsync(command, context, cancellationToken);
        if (!result.IsSuccess)
        {
            return Rejected(result.Error, started);
        }

        BookingTelemetry.RecordCreated(context.ServiceTypeCode, result.Value.DealershipId, clock.GetElapsedTime(started));
        return result;
    }

    // Every request that ends with a domain error is counted once, with its code as the reason.
    private Result<AppointmentDto> Rejected(DomainError error, long started)
    {
        BookingTelemetry.RecordRejected(error, clock.GetElapsedTime(started));
        return Result.Failure<AppointmentDto>(error);
    }

    private async Task<Result<AppointmentDto>> BookAsync(
        BookAppointmentCommand command, BookingContext context, CancellationToken cancellationToken)
    {
        DateTime now = clock.GetLocalNow().DateTime;
        var request = new BookingRequest(
            new CustomerId(command.CustomerId),
            context.Vehicle,
            context.Dealership,
            command.RequestedStart,
            context.ServiceType);

        Result<TimeSlot> slot = BookingValidator.Validate(request, now);
        if (!slot.IsSuccess)
        {
            return Result.Failure<AppointmentDto>(slot.Error);
        }

        // Generated once, so every attempt saves the same appointment (booking-domain.md, Idempotency).
        var appointmentId = new AppointmentId(Guid.CreateVersion7());
        var allocationRequest = new AllocationRequest(context.Dealership, context.ServiceType, slot.Value, BusyIntervals.None);

        // The whole date, not just the slot: workload counts every job on that date (ADR-0002 #20).
        var busyWindow = TimeSlot.Create(slot.Value.Start.Date, WholeDate);

        for (int attempt = 1; ; attempt++)
        {
            Result<Allocation> allocation = await AllocateAsync(allocationRequest, busyWindow, attempt, cancellationToken);
            if (!allocation.IsSuccess)
            {
                return Result.Failure<AppointmentDto>(allocation.Error);
            }

            var appointment = Appointment.Book(appointmentId, request, allocation.Value, now);
            Result<Appointment> saved = await PersistAsync(appointment, attempt, cancellationToken);
            if (saved.IsSuccess)
            {
                return Result.Success(ToDto(saved.Value, context));
            }

            // A vehicle conflict is final, since another technician or bay cannot free the vehicle (Allocation policy 4).
            // Once the attempts run out, the last conflict is the answer (policy 3).
            if (!IsLostRace(saved.Error) || attempt == SlotAllocator.MaxAllocationAttempts)
            {
                return Result.Failure<AppointmentDto>(saved.Error);
            }

            BookingTelemetry.RecordRetry();
            allocationRequest = ExcludeLostResource(allocationRequest, allocation.Value, saved.Error);
        }
    }

    // Busy intervals are read again on every attempt, so a retry sees what it can of the booking that won the race.
    private async Task<Result<Allocation>> AllocateAsync(
        AllocationRequest request, TimeSlot busyWindow, int attempt, CancellationToken cancellationToken)
    {
        using Activity? span = BookingTelemetry.StartAllocate(attempt);

        BusyIntervals busy = await reader.GetBusyIntervalsAsync(request.Dealership.Id, busyWindow, cancellationToken);
        Result<Allocation> allocation = allocator.Allocate(request with { Busy = busy });

        BookingTelemetry.EndAllocate(span, allocation);
        return allocation;
    }

    private async Task<Result<Appointment>> PersistAsync(Appointment appointment, int attempt, CancellationToken cancellationToken)
    {
        using Activity? span = BookingTelemetry.StartPersist(attempt);

        Result<Appointment> saved = await repository.AddAsync(appointment, cancellationToken);

        BookingTelemetry.EndPersist(span, saved);
        return saved;
    }

    // A concurrent booking took the technician or the bay after the busy intervals were read (Allocation policy 3).
    private static bool IsLostRace(DomainError error) =>
        error == BookingErrors.NoQualifiedTechnicianAvailable || error == BookingErrors.NoCompatibleBayAvailable;

    // The busy intervals read next may still miss the winner, so the lost resource is excluded explicitly.
    private static AllocationRequest ExcludeLostResource(AllocationRequest request, Allocation lost, DomainError conflict) =>
        conflict == BookingErrors.NoQualifiedTechnicianAvailable
            ? request with { ExcludedTechnicians = request.ExcludedTechnicians.Add(lost.TechnicianId) }
            : request with { ExcludedBays = request.ExcludedBays.Add(lost.BayId) };

    private static AppointmentDto ToDto(Appointment appointment, BookingContext context)
    {
        var bay = context.Dealership.Bays.Single(candidate => candidate.Id == appointment.ServiceBayId);

        return new AppointmentDto(
            appointment.Id.Value,
            appointment.Status.ToString(),
            appointment.CustomerId.Value,
            appointment.VehicleId.Value,
            appointment.ServiceTypeId.Value,
            appointment.DealershipId.Value,
            appointment.Slot.Start,
            appointment.Slot.End,
            new AppointmentTechnicianDto(appointment.TechnicianId.Value, context.TechnicianNames[appointment.TechnicianId]),
            new AppointmentServiceBayDto(bay.Id.Value, context.BayNames[bay.Id], bay.Type.ToString()));
    }
}
