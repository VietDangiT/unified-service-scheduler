using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>The skills used across the tests.</summary>
internal static class Skills
{
    public static readonly Skill General = new("General");

    public static readonly Skill EV = new("EV");

    public static readonly Skill Alignment = new("Alignment");
}
