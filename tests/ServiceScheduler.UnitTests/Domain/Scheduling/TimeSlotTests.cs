using ServiceScheduler.Domain.Scheduling;

namespace ServiceScheduler.UnitTests.Domain.Scheduling;

public sealed class TimeSlotTests
{
    private static readonly DateOnly Day = new(2026, 11, 3);

    // EC-01: half-open intervals, so touching edges do not overlap.
    [Theory]
    [InlineData("10:00", "11:00", "11:00", "12:00")] // second starts when first ends
    [InlineData("11:00", "12:00", "10:00", "11:00")] // second ends when first starts
    public void Overlaps_WhenSlotsAreBackToBack_ReturnsFalse(
        string firstStart, string firstEnd, string secondStart, string secondEnd)
    {
        var first = Slot(firstStart, firstEnd);
        var second = Slot(secondStart, secondEnd);

        Assert.False(first.Overlaps(second));
        Assert.False(second.Overlaps(first));
    }

    [Fact]
    public void Overlaps_WhenSlotsAreApart_ReturnsFalse()
    {
        var first = Slot("10:00", "11:00");
        var second = Slot("12:00", "13:00");

        Assert.False(first.Overlaps(second));
        Assert.False(second.Overlaps(first));
    }

    // EC-02: any shared instant is an overlap, however small.
    [Theory]
    [InlineData("10:30", "11:30")] // starts inside
    [InlineData("09:30", "10:30")] // ends inside
    [InlineData("10:15", "10:45")] // contained
    [InlineData("09:00", "12:00")] // contains
    [InlineData("10:00", "11:00")] // identical
    [InlineData("10:59", "11:30")] // one minute shared
    public void Overlaps_WhenSlotsShareAnyInstant_ReturnsTrue(string otherStart, string otherEnd)
    {
        var booked = Slot("10:00", "11:00");
        var other = Slot(otherStart, otherEnd);

        Assert.True(booked.Overlaps(other));
        Assert.True(other.Overlaps(booked));
    }

    [Fact]
    public void Create_WithDuration_EndsAtStartPlusDuration()
    {
        var slot = TimeSlot.Create(At("10:00"), TimeSpan.FromMinutes(50));

        Assert.Equal(At("10:50"), slot.EndUtc);
    }

    [Fact]
    public void Create_WithNonUtcOffset_StoresUtc()
    {
        var londonSummerTime = new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.FromHours(1));

        var slot = TimeSlot.Create(londonSummerTime, TimeSpan.FromHours(1));

        Assert.Equal(new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), slot.StartUtc);
        Assert.Equal(TimeSpan.Zero, slot.StartUtc.Offset);
        Assert.Equal(TimeSpan.Zero, slot.EndUtc.Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Create_WithNonPositiveDuration_Throws(int minutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TimeSlot.Create(At("10:00"), TimeSpan.FromMinutes(minutes)));
    }

    private static TimeSlot Slot(string start, string end) =>
        TimeSlot.Create(At(start), At(end) - At(start));

    private static DateTimeOffset At(string time) =>
        new(Day.ToDateTime(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture)), TimeSpan.Zero);
}
