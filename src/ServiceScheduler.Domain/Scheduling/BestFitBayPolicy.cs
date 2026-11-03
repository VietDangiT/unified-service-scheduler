using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>Least specialised compatible bay, then lowest bay ID (ADR-0002 #18).</summary>
public sealed class BestFitBayPolicy : IBayAllocationPolicy
{
    public ServiceBay? Choose(IReadOnlyCollection<ServiceBay> candidates, BayType required)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .Where(bay => bay.CanServe(required))
            .OrderBy(bay => bay.Type)
            .ThenBy(bay => bay.Id.Value)
            .FirstOrDefault();
    }
}
