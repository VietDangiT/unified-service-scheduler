using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>Chooses one bay among the free candidates, or none when no candidate suits the job.</summary>
public interface IBayAllocationPolicy
{
    ServiceBay? Choose(IReadOnlyCollection<ServiceBay> candidates, BayType required);
}
