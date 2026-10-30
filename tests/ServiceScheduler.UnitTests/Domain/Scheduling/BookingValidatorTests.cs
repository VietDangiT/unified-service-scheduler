using System.Globalization;

using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.UnitTests.Builders;

namespace ServiceScheduler.UnitTests.Domain.Scheduling;

public sealed class BookingValidatorTests
{
    // November 2026: London is on GMT, so local time equals UTC.
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateOnly Saturday = new(2026, 11, 7);

    // The advisor is booking at 09:00 on Monday, London time.
    private static readonly DateTimeOffset Now = BookingRequestBuilder.Local(Monday, "09:00");

    // July 2027: London is on British Summer Time (UTC+1).
    private static readonly DateOnly SummerTuesday = new(2027, 7, 6);
    private static readonly DateTimeOffset SummerNow = BookingRequestBuilder.Local(new DateOnly(2027, 7, 1), "09:00");

    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsServiceSlot()
    {
        var request = BookingRequestBuilder.Default().At(Tuesday, "10:00").WithDuration(minutes: 60).Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingRequestBuilder.Local(Tuesday, "10:00"), result.Value.StartUtc);
        Assert.Equal(BookingRequestBuilder.Local(Tuesday, "11:00"), result.Value.EndUtc);
    }

    // Accepted bookings vary along every dimension the rules care about, so a rule that is too strict
    // (rejects same-day work, hourly-only grid, Saturday treated as closed, UTC dates) loses real business.
    // Riverside hours: Mon-Fri 08:00-18:00, Sat 09:00-13:00 (DealershipBuilder default). 60-minute service.
    [Theory]
    [InlineData("2026-11-02T09:15:00+00:00", "Europe/London")] // EC-58 walk-in: today, 15 min from now
    [InlineData("2026-11-02T14:00:00+00:00", "Europe/London")] // EC-58 later today
    [InlineData("2026-11-03T10:15:00+00:00", "Europe/London")] // EC-59 quarter past
    [InlineData("2026-11-03T10:30:00+00:00", "Europe/London")] // EC-59 half past
    [InlineData("2026-11-03T10:45:00+00:00", "Europe/London")] // EC-59 quarter to
    [InlineData("2026-11-07T12:00:00+00:00", "Europe/London")] // EC-60 Saturday, ends at 13:00 closing
    [InlineData("2026-11-10T12:30:00+13:00", "Pacific/Auckland")] // EC-61 Tue 12:30-13:30 local crosses UTC midnight
    [InlineData("2026-11-03T09:00:00+01:00", "Europe/London")] // EC-11 typed in another offset: 08:00 London
    public void Validate_WhenBookingFollowsEveryRule_IsAccepted(string start, string timeZone)
    {
        var requestedStart = Instant(start);
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default().InTimeZone(timeZone).Build())
            .At(requestedStart)
            .Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
        Assert.Equal(requestedStart, result.Value.StartUtc);
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
    public void Validate_WhenStartIsNowOrEarlier_ReturnsStartInPast(string localStart)
    {
        var request = BookingRequestBuilder.Default().At(Monday, localStart).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartInPast, result);
    }

    // EC-09: "past" is about the instant, not the clock time typed in. Auckland Monday 09:30 is
    // Sunday 20:30 UTC, hours before the advisor's Monday 09:00 London.
    [Fact]
    public void Validate_WhenAucklandMorningIsAlreadyOverInLondon_ReturnsStartInPast()
    {
        var request = Auckland().At(Instant("2026-11-02T09:30:00+13:00")).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartInPast, result);
    }

    // EC-10
    [Theory]
    [InlineData("10:05")]
    [InlineData("10:10")]
    [InlineData("10:50")]
    public void Validate_WhenStartIsBetweenQuarterHours_ReturnsStartNotOnGrid(string localStart)
    {
        var request = BookingRequestBuilder.Default().At(Tuesday, localStart).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartNotOnGrid, result);
    }

    // EC-31
    [Theory]
    [InlineData(30_000)] // 10:00:30
    [InlineData(1)] // 10:00:00.001
    public void Validate_WhenStartHasSecondsOrMilliseconds_ReturnsStartNotOnGrid(int milliseconds)
    {
        var start = BookingRequestBuilder.Local(Tuesday, "10:00").AddMilliseconds(milliseconds);
        var request = BookingRequestBuilder.Default().At(start).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartNotOnGrid, result);
    }

    // EC-10: the grid is the dealership's clock, not the clock in the request. 10:00+00:10 is 09:50 in London.
    [Fact]
    public void Validate_WhenRequestOffsetHidesAnOffGridLocalTime_ReturnsStartNotOnGrid()
    {
        var request = BookingRequestBuilder.Default().At(Instant("2026-11-03T10:00:00+00:10")).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartNotOnGrid, result);
    }

    // EC-03: the whole service must fit inside that day's opening hours.
    [Theory]
    [InlineData("17:30", 60)] // ends 18:30, after closing
    [InlineData("07:45", 60)] // starts before opening
    [InlineData("08:00", 615)] // longer than the whole opening day
    public void Validate_WhenServiceDoesNotFitOpeningHours_ReturnsOutsideOpeningHours(string localStart, int minutes)
    {
        var request = OpenOnTuesday("08:00", "18:00").At(Tuesday, localStart).WithDuration(minutes).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // EC-03 boundary: half-open, so starting exactly at opening and finishing exactly at closing are allowed.
    [Theory]
    [InlineData("08:00", 60)]
    [InlineData("17:00", 60)]
    public void Validate_WhenServiceTouchesOpeningOrClosingTime_IsAccepted(string localStart, int minutes)
    {
        var request = OpenOnTuesday("08:00", "18:00").At(Tuesday, localStart).WithDuration(minutes).Build();

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess);
    }

    // EC-03 / EC-60: each weekday has its own hours; Saturday opens later and closes earlier.
    [Theory]
    [InlineData("08:30")] // weekday opening time, but Saturday opens at 09:00
    [InlineData("12:30")] // ends 13:30, after Saturday's 13:00 closing
    public void Validate_WhenSaturdayServiceIsOutsideSaturdayHours_ReturnsOutsideOpeningHours(string localStart)
    {
        var request = BookingRequestBuilder.Default()
            .AtDealership(DealershipBuilder.Default().OpenOn(DayOfWeek.Saturday, "09:00", "13:00").Build())
            .At(Saturday, localStart)
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

    // Opening hours are local wall-clock times: in British Summer Time 08:00 local is 07:00 UTC,
    // so comparing UTC times with opening hours would wrongly reject this booking.
    [Fact]
    public void Validate_DuringSummerTime_AllowsStartAtLocalOpeningTime()
    {
        var request = BookingRequestBuilder.Default().At(SummerTuesday, "08:00").Build();

        var result = BookingValidator.Validate(request, SummerNow);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_DuringSummerTime_RejectsStartBeforeLocalOpeningTime()
    {
        var request = BookingRequestBuilder.Default().At(SummerTuesday, "07:45").Build();

        var result = BookingValidator.Validate(request, SummerNow);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // EC-11: what counts is the instant, not the clock time typed in. In July 07:30 UTC is 08:30 in London
    // (open) and 17:30 UTC is 18:30 (closed). Uses SummerNow so the request stays inside the booking horizon.
    [Fact]
    public void Validate_WhenSummerRequestIsTypedInUtc_IsAcceptedAtLocalTime()
    {
        var request = BookingRequestBuilder.Default().At(Instant("2027-07-06T07:30:00+00:00")).Build();

        var result = BookingValidator.Validate(request, SummerNow);

        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
    }

    [Fact]
    public void Validate_WhenUtcTimeIsAfterClosingInLocalSummerTime_ReturnsOutsideOpeningHours()
    {
        var request = BookingRequestBuilder.Default().At(Instant("2027-07-06T17:30:00+00:00")).Build();

        var result = BookingValidator.Validate(request, SummerNow);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // Auckland is UTC+13 in November, so its weekday differs from the UTC weekday every morning.
    [Fact]
    public void Validate_AucklandMondayMorning_IsBookable()
    {
        var request = Auckland().At(Instant("2026-11-09T09:00:00+13:00")).Build(); // Sunday 20:00 UTC

        var result = BookingValidator.Validate(request, Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_AucklandSundayMorning_IsClosed()
    {
        var request = Auckland().At(Instant("2026-11-08T10:00:00+13:00")).Build(); // Saturday 21:00 UTC

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // A job running past local midnight cannot fit a single day's hours, even when its UTC dates match
    // (Auckland Tue 23:30-00:30 local is Tue 10:30-11:30 UTC).
    [Fact]
    public void Validate_WhenAucklandServiceRunsPastLocalMidnight_ReturnsOutsideOpeningHours()
    {
        var request = Auckland().At(Instant("2026-11-10T23:30:00+13:00")).Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.OutsideOpeningHours, result);
    }

    // EC-34: rules are checked in a fixed order, so the first failing rule wins.
    [Fact]
    public void Validate_WhenStartIsInPastAndOffGrid_ReturnsStartInPast()
    {
        var request = BookingRequestBuilder.Default().At(Monday, "08:10").Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartInPast, result);
    }

    [Fact]
    public void Validate_WhenVehicleNotOwnedAndStartInPast_ReturnsVehicleNotOwnedByCustomer()
    {
        var request = BookingRequestBuilder.Default().WithVehicleOwnedByAnotherCustomer().At(Monday, "08:00").Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.VehicleNotOwnedByCustomer, result);
    }

    [Fact]
    public void Validate_WhenStartIsOffGridAndBeforeOpening_ReturnsStartNotOnGrid()
    {
        var request = BookingRequestBuilder.Default().At(Tuesday, "07:50").Build();

        var result = BookingValidator.Validate(request, Now);

        AssertFails(BookingErrors.StartNotOnGrid, result);
    }

    private static BookingRequestBuilder Auckland() =>
        BookingRequestBuilder.Default().AtDealership(DealershipBuilder.Default().InTimeZone("Pacific/Auckland").Build());

    private static BookingRequestBuilder OpenOnTuesday(string opens, string closes) =>
        BookingRequestBuilder.Default().AtDealership(DealershipBuilder.Default().OpenOn(DayOfWeek.Tuesday, opens, closes).Build());

    private static DateTimeOffset Instant(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static void AssertFails<T>(DomainError expected, Result<T> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
    }
}
