using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>Chooses one technician among the eligible candidates, or none when there is no candidate.</summary>
public interface ITechnicianAllocationPolicy
{
    /// <param name="workload">Blocked time already booked for each candidate on the date of the request.</param>
    Technician? Choose(IReadOnlyCollection<Technician> candidates, IReadOnlyDictionary<TechnicianId, TimeSpan> workload);
}
