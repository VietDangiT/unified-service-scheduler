using System.Globalization;

using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>
/// A valid booking by default: the <see cref="DealershipBuilder"/> default dealership,
/// Tuesday 3 Nov 2026 at 10:00, a 60-minute oil change (<c>General</c> skill, <c>Standard</c> bay).
/// Tests override only the values that matter to them.
/// </summary>
internal sealed class BookingRequestBuilder
{
    private static readonly CustomerId Customer = new(Guid.Parse("c0000000-0000-0000-0000-000000000001"));
    private static readonly CustomerId AnotherCustomer = new(Guid.Parse("c0000000-0000-0000-0000-000000000002"));
    private static readonly VehicleId VehicleId = new(Guid.Parse("e0000000-0000-0000-0000-000000000001"));

    private Dealership _dealership = DealershipBuilder.Default().Build();
    private CustomerId _vehicleOwner = Customer;
    private DateTime _start = TimeOn(new DateOnly(2026, 11, 3), "10:00");
    private ServiceType _serviceType = ServiceTypeBuilder.OilChange().Build();

    public static BookingRequestBuilder Default() => new();

    public static DateTime TimeOn(DateOnly date, string time) =>
        date.ToDateTime(TimeOnly.Parse(time, CultureInfo.InvariantCulture));

    public BookingRequestBuilder AtDealership(Dealership dealership)
    {
        _dealership = dealership;
        return this;
    }

    public BookingRequestBuilder At(DateOnly date, string time) => At(TimeOn(date, time));

    public BookingRequestBuilder At(DateTime start)
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
