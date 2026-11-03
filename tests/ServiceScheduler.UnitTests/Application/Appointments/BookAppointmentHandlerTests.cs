using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.UnitTests.Application.Fakes;
using ServiceScheduler.UnitTests.Builders;

namespace ServiceScheduler.UnitTests.Application.Appointments;

// A save that fails on a technician or a bay means a concurrent booking took it between the read and the save
// (booking-domain.md, Allocation policy 3 and 4). The reader never sees that winner, so only excluding the resource
// that conflicted can steer the next attempt away from it. Every resource is a {General} technician or a Standard bay,
// so without the exclusion the lowest ID would be chosen again.
public sealed class BookAppointmentHandlerTests
{
    // The default booking is Tuesday 3 Nov 2026 10:00, a 60-minute oil change; now is the day before.
    private static readonly DateTime Now = BookingRequestBuilder.TimeOn(new DateOnly(2026, 11, 2), "09:00");

    private static readonly Technician FirstTechnician = TechnicianBuilder.Default().WithId(1).Build();
    private static readonly Technician SecondTechnician = TechnicianBuilder.Default().WithId(2).Build();
    private static readonly Technician ThirdTechnician = TechnicianBuilder.Default().WithId(3).Build();
    private static readonly ServiceBay FirstBay = ServiceBayBuilder.Default().WithId(1).Build();
    private static readonly ServiceBay SecondBay = ServiceBayBuilder.Default().WithId(2).Build();
    private static readonly ServiceBay ThirdBay = ServiceBayBuilder.Default().WithId(3).Build();

    // EC-18: the first technician was taken by a concurrent booking; the second is free, and the bay still is.
    [Fact]
    public async Task HandleAsync_WhenTheSaveLosesTheRaceOnTheTechnician_BooksTheNextQualifiedTechnicianWithTheSameAppointment()
    {
        var dealership = DealershipWith([FirstTechnician, SecondTechnician], [FirstBay, SecondBay]);
        var repository = new ScriptedAppointmentRepository(BookingErrors.NoQualifiedTechnicianAvailable);

        Result<AppointmentDto> result = await BookAsync(dealership, repository);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(SecondTechnician.Id.Value, result.Value.Technician.Id);
        Assert.Equal(FirstBay.Id.Value, result.Value.ServiceBay.Id);
        Assert.Collection(
            repository.Attempts,
            first => Assert.Equal(FirstTechnician.Id, first.TechnicianId),
            second => Assert.Equal(SecondTechnician.Id, second.TechnicianId));
        Assert.Single(repository.Attempts.Select(attempt => attempt.Id).Distinct());
    }

    // EC-91: the first bay was taken by a concurrent booking; the second is free, and the technician still is.
    [Fact]
    public async Task HandleAsync_WhenTheSaveLosesTheRaceOnTheBay_BooksTheNextCompatibleBay()
    {
        var dealership = DealershipWith([FirstTechnician, SecondTechnician], [FirstBay, SecondBay]);
        var repository = new ScriptedAppointmentRepository(BookingErrors.NoCompatibleBayAvailable);

        Result<AppointmentDto> result = await BookAsync(dealership, repository);

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal(SecondBay.Id.Value, result.Value.ServiceBay.Id);
        Assert.Equal(FirstTechnician.Id.Value, result.Value.Technician.Id);
        Assert.Collection(
            repository.Attempts,
            first => Assert.Equal(FirstBay.Id, first.ServiceBayId),
            second => Assert.Equal(SecondBay.Id, second.ServiceBayId));
    }

    // EC-90: another technician and bay are free, but the vehicle itself is taken, so trying again cannot help (policy 4).
    [Fact]
    public async Task HandleAsync_WhenTheSaveFindsTheVehicleAlreadyBooked_ReturnsVehicleAlreadyBookedWithoutTryingAgain()
    {
        var dealership = DealershipWith([FirstTechnician, SecondTechnician], [FirstBay, SecondBay]);
        var repository = new ScriptedAppointmentRepository(BookingErrors.VehicleAlreadyBooked);

        Result<AppointmentDto> result = await BookAsync(dealership, repository);

        Assert.False(result.IsSuccess);
        Assert.Equal(BookingErrors.VehicleAlreadyBooked, result.Error);
        Assert.Single(repository.Attempts);
    }

    // EC-44: three conflicts in a row use up MaxAllocationAttempts (3). A fourth pair is still free, so stopping is
    // the bound and not a lack of resources. The error is the one of the last conflict; in each row the first conflict
    // is on the other resource, so returning the first conflict instead would give the wrong error.
    [Theory]
    [InlineData("technician", "bay", "bay")] // NoCompatibleBayAvailable
    [InlineData("bay", "technician", "technician")] // NoQualifiedTechnicianAvailable
    public async Task HandleAsync_WhenEverySaveLosesTheRace_StopsAfterThreeAttemptsWithTheCapacityErrorOfTheLastConflict(
        string firstConflict, string secondConflict, string lastConflict)
    {
        var dealership = DealershipWith([FirstTechnician, SecondTechnician, ThirdTechnician], [FirstBay, SecondBay, ThirdBay]);
        var repository = new ScriptedAppointmentRepository(
            ConflictOn(firstConflict), ConflictOn(secondConflict), ConflictOn(lastConflict));

        Result<AppointmentDto> result = await BookAsync(dealership, repository);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConflictOn(lastConflict), result.Error);
        Assert.Equal(3, repository.Attempts.Count);
    }

    private static DomainError ConflictOn(string resource) => resource switch
    {
        "technician" => BookingErrors.NoQualifiedTechnicianAvailable,
        "bay" => BookingErrors.NoCompatibleBayAvailable,
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Expected 'technician' or 'bay'."),
    };

    private static Dealership DealershipWith(Technician[] technicians, ServiceBay[] bays) =>
        DealershipBuilder.Default().WithTechnicians(technicians).WithBays(bays).Build();

    // Nothing is busy as far as the reader knows, on every read.
    private static Task<Result<AppointmentDto>> BookAsync(Dealership dealership, ScriptedAppointmentRepository repository)
    {
        BookingRequest request = BookingRequestBuilder.Default().AtDealership(dealership).Build();
        var reader = new FakeSchedulingReader(ContextOf(request), BusyIntervalsBuilder.None().Build());
        var allocator = new SlotAllocator(new BestFitBayPolicy(), new BestFitTechnicianPolicy());
        var handler = new BookAppointmentHandler(reader, repository, allocator, new FixedTimeProvider(Now));

        return handler.HandleAsync(CommandOf(request), CancellationToken.None);
    }

    private static BookAppointmentCommand CommandOf(BookingRequest request) =>
        new(
            request.CustomerId.Value,
            request.Vehicle.Id.Value,
            request.ServiceType.Id.Value,
            request.Dealership.Id.Value,
            request.RequestedStart);

    private static BookingContext ContextOf(BookingRequest request) =>
        new(
            request.Vehicle,
            request.ServiceType,
            request.Dealership,
            request.Dealership.Technicians.ToDictionary(technician => technician.Id, technician => $"Technician {technician.Id.Value}"),
            request.Dealership.Bays.ToDictionary(bay => bay.Id, bay => $"Bay {bay.Id.Value}"));
}
