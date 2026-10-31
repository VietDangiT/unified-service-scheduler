using System.Globalization;

using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.UnitTests.Builders;

namespace ServiceScheduler.UnitTests.Domain.Scheduling;

public sealed class BookingValidatorTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateOnly Saturday = new(2026, 11, 7);

    // The advisor is booking at 09:00 on Monday.
    private static readonly DateTime Now = BookingRequestBuilder.TimeOn(Monday, "09:00");

    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsServiceSlot()
    {
        var request = BookingRequestBuilder.Default().At(Tuesday, "10:00").WithDuration(minutes: 60).Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingRequestBuilder.TimeOn(Tuesday, "10:00"), result.Value.Start);
        Assert.Equal(BookingRequestBuilder.TimeOn(Tuesday, "11:00"), result.Value.End);
    }

    // Accepted bookings vary along every dimension the rules care about, so a rule that is too strict
    // (rejects same-day work, hourly-only grid, Saturday treated as closed) loses real business.
    // Riverside hours: Mon-Fri 08:00-18:00, Sat 09:00-13:00 (DealershipBuilder default). 60-minute service.
    [Theory]
    [InlineData("2026-11-02T09:15:00")] // EC-58 walk-in: today, 15 min from now
    [InlineData("2026-11-02T14:00:00")] // EC-58 later today
    [InlineData("2026-11-03T10:15:00")] // EC-59 quarter past
    [InlineData("2026-11-03T10:30:00")] // EC-59 half past
    [InlineData("2026-11-03T10:45:00")] // EC-59 quarter to
    [InlineData("2026-11-07T12:00:00")] // EC-60 Saturday, ends at 13:00 closing
    public void Validate_WhenBookingFollowsEveryRule_IsAccepted(string start)
    {
        var requestedStart = DateTime.Parse(start, CultureInfo.InvariantCulture);
        var request = BookingRequestBuilder.Default().At(requestedStart).Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
        Assert.Equal(requestedStart, result.Value.Start);
    }

    // EC-20
    [Fact]
    public void Validate_WhenVehicleBelongsToAnotherCustomer_ReturnsVehicleNotOwnedByCustomer()
    {
        var request = BookingRequestBuilder.Default().WithVehicleOwnedByAnotherCustomer().Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.VehicleNotOwnedByCustomer, result);
    }

    // EC-09: a booking for the current minute is already too late.
    [Theory]
    [InlineData("08:00")]
    [InlineData("09:00")]
    public void Validate_WhenStartIsNowOrEarlier_ReturnsStartInPast(string start)
    {
        var request = BookingRequestBuilder.Default().At(Monday, start).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartInPast, result);
    }

    // EC-10
    [Theory]
    [InlineData("10:05")]
    [InlineData("10:10")]
    [InlineData("10:50")]
    public void Validate_WhenStartIsBetweenQuarterHours_ReturnsStartNotOnGrid(string start)
    {
        var request = BookingRequestBuilder.Default().At(Tuesday, start).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartNotOnGrid, result);
    }

    // EC-31
    [Theory]
    [InlineData(30_000)] // 10:00:30
    [InlineData(1)] // 10:00:00.001
    public void Validate_WhenStartHasSecondsOrMilliseconds_ReturnsStartNotOnGrid(int milliseconds)
    {
        var start = BookingRequestBuilder.TimeOn(Tuesday, "10:00").AddMilliseconds(milliseconds);
        var request = BookingRequestBuilder.Default().At(start).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartNotOnGrid, result);
    }

    // EC-03: the whole service must fit inside that day's opening hours.
    [Theory]
    [InlineData("17:30", 60)] // ends 18:30, after closing
    [InlineData("07:45", 60)] // starts before opening
    [InlineData("08:00", 615)] // longer than the whole opening day
    public void Validate_WhenServiceDoesNotFitOpeningHours_ReturnsOutsideOpeningHours(string start, int minutes)
    {
        var request = OpenOnTuesday("08:00", "18:00").At(Tuesday, start).WithDuration(minutes).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // EC-03 boundary: half-open, so starting exactly at opening and finishing exactly at closing are allowed.
    [Theory]
    [InlineData("08:00", 60)]
    [InlineData("17:00", 60)]
    public void Validate_WhenServiceTouchesOpeningOrClosingTime_IsAccepted(string start, int minutes)
    {
        var request = OpenOnTuesday("08:00", "18:00").At(Tuesday, start).WithDuration(minutes).Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess);
    }

    // EC-03 / EC-60: each weekday has its own hours; Saturday opens later and closes earlier.
    [Theory]
    [InlineData("08:30")] // weekday opening time, but Saturday opens at 09:00
    [InlineData("12:30")] // ends 13:30, after Saturday's 13:00 closing
    public void Validate_WhenSaturdayServiceIsOutsideSaturdayHours_ReturnsOutsideOpeningHours(string start)
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default().OpenOn(DayOfWeek.Saturday, "09:00", "13:00").Build())
            .At(Saturday, start)
            .Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // EC-33
    [Fact]
    public void Validate_WhenDealershipIsClosedThatWeekday_ReturnsOutsideOpeningHours()
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default().ClosedOn(DayOfWeek.Sunday).Build())
            .At(new DateOnly(2026, 11, 8), "10:00")
            .Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // EC-22: rule 3 is a static capability check. An EV job needs an EV bay; a Standard and a Lift bay do not count.
    [Fact]
    public void Validate_WhenDealershipHasNoBayOfTheRequiredType_ReturnsServiceNotOfferedAtDealership()
    {
        var request = EvCheckAt(
            DealershipBuilder.Default()
                .WithTechnicians(TechnicianBuilder.Default().WithSkills(Skills.General, Skills.EV).Build())
                .WithBays(
                    ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build(),
                    ServiceBayBuilder.Default().WithId(2).OfType(BayType.Lift).Build()));

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.ServiceNotOfferedAtDealership, result);
    }

    // EC-22: skills match exactly; a General-only technician cannot do an EV job.
    [Fact]
    public void Validate_WhenNoTechnicianHoldsTheRequiredSkill_ReturnsServiceNotOfferedAtDealership()
    {
        var request = EvCheckAt(
            DealershipBuilder.Default()
                .WithTechnicians(TechnicianBuilder.Default().WithSkills(Skills.General).Build())
                .WithBays(ServiceBayBuilder.Default().OfType(BayType.EV).Build()));

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.ServiceNotOfferedAtDealership, result);
    }

    // EC-22: only active resources count.
    [Fact]
    public void Validate_WhenOnlyCompatibleBayIsInactive_ReturnsServiceNotOfferedAtDealership()
    {
        var request = EvCheckAt(
            DealershipBuilder.Default()
                .WithTechnicians(TechnicianBuilder.Default().WithSkills(Skills.General, Skills.EV).Build())
                .WithBays(
                    ServiceBayBuilder.Default().WithId(1).OfType(BayType.EV).Inactive().Build(),
                    ServiceBayBuilder.Default().WithId(2).OfType(BayType.Standard).Build()));

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.ServiceNotOfferedAtDealership, result);
    }

    [Fact]
    public void Validate_WhenOnlySkilledTechnicianIsInactive_ReturnsServiceNotOfferedAtDealership()
    {
        var request = EvCheckAt(
            DealershipBuilder.Default()
                .WithTechnicians(
                    TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General, Skills.EV).Inactive().Build(),
                    TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General).Build())
                .WithBays(ServiceBayBuilder.Default().OfType(BayType.EV).Build()));

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.ServiceNotOfferedAtDealership, result);
    }

    // EC-22: no resources of the kind at all.
    [Fact]
    public void Validate_WhenDealershipHasNoBays_ReturnsServiceNotOfferedAtDealership()
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default().WithoutBays().Build())
            .Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.ServiceNotOfferedAtDealership, result);
    }

    [Fact]
    public void Validate_WhenDealershipHasNoTechnicians_ReturnsServiceNotOfferedAtDealership()
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default().WithoutTechnicians().Build())
            .Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.ServiceNotOfferedAtDealership, result);
    }

    // EC-62: a Standard job can use any bay type (ADR-0002 #9), so Lift and EV bays make it offered.
    [Fact]
    public void Validate_WhenStandardJobAndOnlyLiftAndEvBaysExist_IsAccepted()
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default()
                .WithBays(
                    ServiceBayBuilder.Default().WithId(1).OfType(BayType.Lift).Build(),
                    ServiceBayBuilder.Default().WithId(2).OfType(BayType.EV).Build())
                .Build())
            .Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
    }

    // EC-69: rule 3 counts active technicians with the skill; shifts are not considered (ADR-0002 #19).
    [Fact]
    public void Validate_WhenSkilledTechnicianHasNoShifts_IsAccepted()
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default()
                .WithTechnicians(TechnicianBuilder.Default().WithNoShifts().Build())
                .Build())
            .Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
    }

    // EC-24: a shift that starts before opening does not extend the bookable window.
    [Fact]
    public void Validate_WhenBookingIsBeforeOpeningButInsideTechniciansShift_ReturnsOutsideOpeningHours()
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default()
                .OpenOn(DayOfWeek.Tuesday, "08:00", "18:00")
                .WithTechnicians(TechnicianBuilder.Default().OnShift(DayOfWeek.Tuesday, "07:00", "19:00").Build())
                .Build())
            .At(Tuesday, "07:30")
            .Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // EC-71: specialised jobs are offered when the dealership has the matching technician and bay (ADR-0002 #9),
    // even when a generalist and a Standard bay are listed first.
    [Fact]
    public void Validate_WhenEvJobAtDealershipWithMixedStaffAndBays_IsAccepted()
    {
        var request = EvCheckAt(
            DealershipBuilder.Default()
                .WithTechnicians(
                    TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build(),
                    TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.EV).Build())
                .WithBays(
                    ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build(),
                    ServiceBayBuilder.Default().WithId(2).OfType(BayType.EV).Build()));

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
    }

    [Fact]
    public void Validate_WhenAlignmentJobAtDealershipWithMixedStaffAndBays_IsAccepted()
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default()
                .WithTechnicians(
                    TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build(),
                    TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.Alignment).Build())
                .WithBays(
                    ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build(),
                    ServiceBayBuilder.Default().WithId(2).OfType(BayType.Alignment).Build())
                .Build())
            .WithServiceType(ServiceTypeBuilder.WheelAlignment().Build())
            .Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
    }

    private static BookingRequest EvCheckAt(DealershipBuilder dealership) =>
        BookingRequestBuilder.Default()
            .AtDealership(dealership.Build())
            .WithServiceType(ServiceTypeBuilder.EvCheck().Build())
            .Build();

    private static BookingRequestBuilder OpenOnTuesday(string opens, string closes) =>
        BookingRequestBuilder.Default().AtDealership(DealershipBuilder.Default().OpenOn(DayOfWeek.Tuesday, opens, closes).Build());

    private static void AssertFails<T>(DomainError expected, Result<T> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
    }
}
