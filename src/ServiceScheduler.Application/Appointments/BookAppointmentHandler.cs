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

        // The whole date, not just the slot: workload counts every job on that date (ADR-0002 #20).
        BusyIntervals busy = await reader.GetBusyIntervalsAsync(
            context.Dealership.Id,
            TimeSlot.Create(slot.Value.Start.Date, WholeDate),
            cancellationToken);

        Result<Allocation> allocation = allocator.Allocate(
            new AllocationRequest(context.Dealership, context.ServiceType, slot.Value, busy));
        if (!allocation.IsSuccess)
        {
            return Result.Failure<AppointmentDto>(allocation.Error);
        }

        var appointment = Appointment.Book(new AppointmentId(Guid.CreateVersion7()), request, allocation.Value, now);
        await repository.AddAsync(appointment, cancellationToken);

        return Result.Success(ToDto(appointment, context));
    }

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
