using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.ServiceTypes;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>
/// Presets, all 60 minutes: oil change (<c>General</c> skill, <c>Standard</c> bay), EV check (<c>EV</c>, <c>EV</c>),
/// wheel alignment (<c>Alignment</c>, <c>Alignment</c>). Tests override only the values that matter to them.
/// </summary>
internal sealed class ServiceTypeBuilder
{
    private readonly ServiceTypeId _id;
    private readonly Skill _requiredSkill;
    private readonly BayType _requiredBayType;
    private TimeSpan _duration = TimeSpan.FromMinutes(60);

    private ServiceTypeBuilder(int number, Skill requiredSkill, BayType requiredBayType)
    {
        _id = new ServiceTypeId(Guid.Parse($"5e000000-0000-0000-0000-{number:D12}"));
        _requiredSkill = requiredSkill;
        _requiredBayType = requiredBayType;
    }

    public static ServiceTypeBuilder OilChange() => new(1, Skills.General, BayType.Standard);

    public static ServiceTypeBuilder EvCheck() => new(2, Skills.EV, BayType.EV);

    public static ServiceTypeBuilder WheelAlignment() => new(3, Skills.Alignment, BayType.Alignment);

    public ServiceTypeBuilder WithDuration(int minutes)
    {
        _duration = TimeSpan.FromMinutes(minutes);
        return this;
    }

    public ServiceType Build() => new(_id, _duration, _requiredSkill, _requiredBayType);
}
