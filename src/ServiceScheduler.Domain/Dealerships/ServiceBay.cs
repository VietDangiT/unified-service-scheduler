namespace ServiceScheduler.Domain.Dealerships;

public sealed record ServiceBay(ServiceBayId Id, DealershipId DealershipId, BayType Type, bool IsActive)
{
    /// <summary>A <see cref="BayType.Standard"/> job can use any bay; every other job needs its exact type (ADR-0002 #9).</summary>
    public bool CanServe(BayType required) => required == BayType.Standard || Type == required;
}
