using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.UnitTests.Builders;

/// <summary>Bay 1 at Riverside by default: active, <c>Standard</c>. IDs are readable: <c>ba000000-...-00000000000N</c>.</summary>
internal sealed class ServiceBayBuilder
{
    private int _number = 1;
    private DealershipId _dealershipId = DealershipBuilder.RiversideId;
    private BayType _type = BayType.Standard;
    private bool _isActive = true;

    public static ServiceBayBuilder Default() => new();

    public static ServiceBayId IdOf(int number) => new(Guid.Parse($"ba000000-0000-0000-0000-{number:D12}"));

    public ServiceBayBuilder WithId(int number)
    {
        _number = number;
        return this;
    }

    public ServiceBayBuilder OfType(BayType type)
    {
        _type = type;
        return this;
    }

    public ServiceBayBuilder Inactive()
    {
        _isActive = false;
        return this;
    }

    public ServiceBayBuilder AtAnotherDealership()
    {
        _dealershipId = DealershipBuilder.AnotherDealershipId;
        return this;
    }

    public ServiceBay Build() => new(IdOf(_number), _dealershipId, _type, _isActive);
}
