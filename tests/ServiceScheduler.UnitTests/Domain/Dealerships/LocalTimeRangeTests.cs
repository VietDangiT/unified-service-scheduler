using System.Globalization;

using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.UnitTests.Domain.Dealerships;

public sealed class LocalTimeRangeTests
{
    // EC-57 (domain side): a range must end after it starts and never crosses midnight.
    [Theory]
    [InlineData("18:00", "08:00")] // inverted
    [InlineData("08:00", "08:00")] // empty
    public void Constructor_WhenEndIsNotAfterStart_Throws(string start, string end)
    {
        Assert.Throws<ArgumentException>(() => new LocalTimeRange(Time(start), Time(end)));
    }

    [Fact]
    public void Constructor_WhenEndIsAfterStart_KeepsBothTimes()
    {
        var range = new LocalTimeRange(Time("08:00"), Time("08:15"));

        Assert.Equal(Time("08:00"), range.Start);
        Assert.Equal(Time("08:15"), range.End);
    }

    private static TimeOnly Time(string value) => TimeOnly.Parse(value, CultureInfo.InvariantCulture);
}
