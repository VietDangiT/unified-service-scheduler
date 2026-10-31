using System.Globalization;

using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>
/// A valid booking by default: the <see cref="DealershipBuilder"/> default dealership,
/// Tuesday 3 Nov 2026 at 10:00 London time, a 60-minute oil change (<c>General</c> skill, <c>Standard</c> bay).
/// Tests override only the values that matter to them.
/// </summary>
internal sealed class BookingRequestBuilder
{
    private static readonly CustomerId Customer = new(Guid.Parse("c0000000-0000-0000-0000-000000000001"));
    private static readonly CustomerId AnotherCustomer = new(Guid.Parse("c0000000-0000-0000-0000-000000000002"));
    private static readonly VehicleId VehicleId = new(Guid.Parse("e0000000-0000-0000-0000-000000000001"));

    private Dealership _dealership = DealershipBuilder.Default().Build();
    private CustomerId _vehicleOwner = Customer;
    private DateTimeOffset _start = Local(new DateOnly(2026, 11, 3), "10:00");
    private ServiceType _serviceType = ServiceTypeBuilder.OilChange().Build();

    public static BookingRequestBuilder Default() => new();

    /// <summary>A London wall-clock time, converted with the offset in force on that date.</summary>
    /// <exception cref="ArgumentException">The time is skipped or repeated by a clock change; such
    /// instants are ambiguous, so a test must state the exact offset instead.</exception>
    public static DateTimeOffset Local(DateOnly date, string time)
    {
        var london = DealershipBuilder.London;
        var local = date.ToDateTime(TimeOnly.Parse(time, CultureInfo.InvariantCulture));
        if (london.IsInvalidTime(local) || london.IsAmbiguousTime(local))
        {
            throw new ArgumentException($"{date:yyyy-MM-dd} {time} is skipped or repeated in London; pass a DateTimeOffset.", nameof(time));
        }

        return new DateTimeOffset(local, london.GetUtcOffset(local));
    }

    public BookingRequestBuilder AtDealership(Dealership dealership)
    {
        _dealership = dealership;
        return this;
    }

    public BookingRequestBuilder At(DateOnly date, string londonTime) => At(Local(date, londonTime));

    public BookingRequestBuilder At(DateTimeOffset start)
    {
        _start = start;
        return this;
    }

    /// <summary>An oil change (<c>General</c> skill, <c>Standard</c> bay) of the given length.</summary>
    public BookingRequestBuilder WithDuration(int minutes)
    {
        _serviceType = ServiceTypeBuilder.OilChange().WithDuration(minutes).Build();
        return this;
    }

    public BookingRequestBuilder WithServiceType(ServiceType serviceType)
    {
        _serviceType = serviceType;
        return this;
    }

    public BookingRequestBuilder WithVehicleOwnedByAnotherCustomer()
    {
        _vehicleOwner = AnotherCustomer;
        return this;
    }

    public BookingRequest Build() =>
        new(Customer, new Vehicle(VehicleId, _vehicleOwner), _dealership, _start, _serviceType);
}
