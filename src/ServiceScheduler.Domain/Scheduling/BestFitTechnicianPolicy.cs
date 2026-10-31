using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>Fewest skills held, then least loaded on the local date, then lowest technician ID (ADR-0002 #2, #20).</summary>
public sealed class BestFitTechnicianPolicy : ITechnicianAllocationPolicy
{
    public Technician? Choose(IReadOnlyCollection<Technician> candidates, IReadOnlyDictionary<TechnicianId, TimeSpan> workload)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(workload);

        return candidates
            .OrderBy(technician => technician.Skills.Count)
            .ThenBy(technician => workload.GetValueOrDefault(technician.Id))
            .ThenBy(technician => technician.Id.Value)
            .FirstOrDefault();
    }
}
