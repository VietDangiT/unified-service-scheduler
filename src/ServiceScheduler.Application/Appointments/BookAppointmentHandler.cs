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

        BookingContext? context = await reader.GetBookingContextAsync(command, cancellationToken);
        if (context is null)
        {
            return Result.Failure<AppointmentDto>(BookingErrors.NotFound);
        }

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
            allocationRequest = allocationRequest with
            {
                Busy = await reader.GetBusyIntervalsAsync(context.Dealership.Id, busyWindow, cancellationToken),
            };

            Result<Allocation> allocation = allocator.Allocate(allocationRequest);
            if (!allocation.IsSuccess)
            {
                return Result.Failure<AppointmentDto>(allocation.Error);
            }

            var appointment = Appointment.Book(appointmentId, request, allocation.Value, now);
            Result<Appointment> saved = await repository.AddAsync(appointment, cancellationToken);
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

            allocationRequest = ExcludeLostResource(allocationRequest, allocation.Value, saved.Error);
        }
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
