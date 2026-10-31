using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Dealerships;

namespace ServiceScheduler.Domain.Scheduling;

/// <summary>
/// Chooses a technician and a bay that are both free for a validated service interval, or returns the
/// capacity error (see .claude/rules/booking-domain.md, Capacity rules and Allocation policy).
/// </summary>
public sealed class SlotAllocator
{
    private readonly IBayAllocationPolicy _bayPolicy;
    private readonly ITechnicianAllocationPolicy _technicianPolicy;

    public SlotAllocator(IBayAllocationPolicy bayPolicy, ITechnicianAllocationPolicy technicianPolicy)
    {
        ArgumentNullException.ThrowIfNull(bayPolicy);
        ArgumentNullException.ThrowIfNull(technicianPolicy);

        _bayPolicy = bayPolicy;
        _technicianPolicy = technicianPolicy;
    }

    public Result<Allocation> Allocate(AllocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var technician = _technicianPolicy.Choose(EligibleTechnicians(request), WorkloadOnDate(request));
        var bay = _bayPolicy.Choose(FreeBays(request), request.ServiceType.RequiredBayType);

        // The technician error takes precedence when both are missing (ADR-0002 #17).
        if (technician is null)
        {
            return Result.Failure<Allocation>(BookingErrors.NoQualifiedTechnicianAvailable);
        }

        if (bay is null)
        {
            return Result.Failure<Allocation>(BookingErrors.NoCompatibleBayAvailable);
        }

        return Result.Success(new Allocation(bay.Id, technician.Id, request.Slot));
    }

    private static Technician[] EligibleTechnicians(AllocationRequest request) =>
    [
        .. request.Dealership.Technicians.Where(technician =>
            technician.IsActive
            && technician.IsQualifiedFor(request.ServiceType.RequiredSkill)
            && technician.IsOnShiftFor(request.Slot)
            && !request.Busy.Technicians.Any(busy => busy.TechnicianId == technician.Id && busy.Blocked.Overlaps(request.Slot))),
    ];

    private static ServiceBay[] FreeBays(AllocationRequest request) =>
    [
        .. request.Dealership.Bays.Where(bay =>
            bay.IsActive
            && !request.Busy.Bays.Any(busy => busy.BayId == bay.Id && busy.Blocked.Overlaps(request.Slot))),
    ];

    // A job belongs to the date it starts on: appointments never span midnight.
    private static Dictionary<TechnicianId, TimeSpan> WorkloadOnDate(AllocationRequest request)
    {
        var date = request.Slot.Start.Date;

        return request.Busy.Technicians
            .Where(busy => busy.Blocked.Start.Date == date)
            .GroupBy(busy => busy.TechnicianId)
            .ToDictionary(
                jobs => jobs.Key,
                jobs => jobs.Aggregate(TimeSpan.Zero, (total, job) => total + job.Blocked.Duration));
    }
}
