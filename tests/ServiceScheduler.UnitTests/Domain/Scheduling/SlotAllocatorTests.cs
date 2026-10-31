using System.Globalization;

using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Domain.ServiceTypes;
using ServiceScheduler.UnitTests.Builders;

namespace ServiceScheduler.UnitTests.Domain.Scheduling;

// IDs are deliberately set against the expected choice where possible (the right answer has the higher ID,
// or is listed last), so a missing rule cannot be hidden by the ID tie-break or by collection order.
public sealed class SlotAllocatorTests
{
    // November 2026: London is on GMT. Riverside (DealershipBuilder default) opens Mon-Fri 08:00-18:00,
    // Sat 09:00-13:00; technicians work those hours unless a test says otherwise.
    private static readonly DateOnly Tuesday = new(2026, 11, 3);
    private static readonly DateOnly Saturday = new(2026, 11, 7);

    private static readonly ServiceType OilChange = ServiceTypeBuilder.OilChange().Build(); // 60 min, General, Standard
    private static readonly ServiceType EvCheck = ServiceTypeBuilder.EvCheck().Build(); // 60 min, EV, EV

    // Every test books 10:00-11:00 on Tuesday unless it says otherwise.
    private static readonly DateTimeOffset TuesdayTen = London(Tuesday, "10:00");

    // EC-01: half-open intervals; a job that ends as the service starts, or starts as it ends, does not block.
    [Theory]
    [InlineData("09:00")] // other job 09:00-10:00
    [InlineData("11:00")] // other job 11:00-12:00
    public void Allocate_WhenTechniciansJobsAreBackToBack_ChoosesThatTechnician(string otherJobStart)
    {
        var technician = TechnicianBuilder.Default().Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None().TechnicianBusy(technician, London(Tuesday, otherJobStart), 60).Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, TuesdayTen, busy);

        AssertAllocated(technician, bay, result);
    }

    [Theory]
    [InlineData("09:00")]
    [InlineData("11:00")]
    public void Allocate_WhenBaysJobsAreBackToBack_ChoosesThatBay(string otherJobStart)
    {
        var technician = TechnicianBuilder.Default().Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None().BayBusy(bay, London(Tuesday, otherJobStart), 60).Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, TuesdayTen, busy);

        AssertAllocated(technician, bay, result);
    }

    // EC-02: the generalist would win on skills, but is busy for part of the service.
    [Fact]
    public void Allocate_WhenBestFitTechnicianIsBusyForPartOfService_ChoosesAnotherQualifiedTechnician()
    {
        var generalist = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build();
        var evTechnician = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.EV).Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None().TechnicianBusy(generalist, London(Tuesday, "09:30"), 60).Build();

        var result = Allocate(Riverside([generalist, evTechnician], [bay]), OilChange, TuesdayTen, busy);

        AssertAllocated(evTechnician, bay, result);
    }

    // EC-02: any shared instant blocks the resource.
    [Theory]
    [InlineData("09:30", 60)] // overlaps the start
    [InlineData("10:30", 60)] // overlaps the end
    [InlineData("10:15", 30)] // inside the service
    [InlineData("09:00", 180)] // covers the whole service
    public void Allocate_WhenOnlyTechnicianHasAnOverlappingJob_ReturnsNoQualifiedTechnicianAvailable(string otherJobStart, int otherJobMinutes)
    {
        var technician = TechnicianBuilder.Default().Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None().TechnicianBusy(technician, London(Tuesday, otherJobStart), otherJobMinutes).Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, TuesdayTen, busy);

        AssertFails(BookingErrors.NoQualifiedTechnicianAvailable, result);
    }

    [Fact]
    public void Allocate_WhenBestFitBayIsBusyForPartOfService_ChoosesAnotherCompatibleBay()
    {
        var technician = TechnicianBuilder.Default().Build();
        var busyBay = ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build();
        var freeBay = ServiceBayBuilder.Default().WithId(2).OfType(BayType.Standard).Build();
        var busy = BusyIntervalsBuilder.None().BayBusy(busyBay, London(Tuesday, "09:30"), 60).Build();

        var result = Allocate(Riverside([technician], [busyBay, freeBay]), OilChange, TuesdayTen, busy);

        AssertAllocated(technician, freeBay, result);
    }

    [Theory]
    [InlineData("09:30", 60)]
    [InlineData("10:30", 60)]
    [InlineData("10:15", 30)]
    [InlineData("09:00", 180)]
    public void Allocate_WhenOnlyBayHasAnOverlappingJob_ReturnsNoCompatibleBayAvailable(string otherJobStart, int otherJobMinutes)
    {
        var technician = TechnicianBuilder.Default().Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None().BayBusy(bay, London(Tuesday, otherJobStart), otherJobMinutes).Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, TuesdayTen, busy);

        AssertFails(BookingErrors.NoCompatibleBayAvailable, result);
    }

    // EC-04: the whole service must fit the technician's shift, not just its start. Riverside is open
    // 08:00-18:00, so each request is inside opening hours.
    [Theory]
    [InlineData("08:00", "17:00", "16:30")] // would end 17:30, after the shift
    [InlineData("09:00", "18:00", "08:00")] // starts before the shift
    public void Allocate_WhenServiceDoesNotFitOnlyTechniciansShift_ReturnsNoQualifiedTechnicianAvailable(
        string shiftStart, string shiftEnd, string serviceStart)
    {
        var technician = TechnicianBuilder.Default().OnShift(DayOfWeek.Tuesday, shiftStart, shiftEnd).Build();
        var bay = ServiceBayBuilder.Default().Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, London(Tuesday, serviceStart));

        AssertFails(BookingErrors.NoQualifiedTechnicianAvailable, result);
    }

    // EC-04 boundary: half-open shifts, so starting as the shift starts and ending as it ends are allowed.
    [Theory]
    [InlineData("08:00", "17:00", "16:00")] // ends exactly at the end of the shift
    [InlineData("09:00", "18:00", "09:00")] // starts exactly at the start of the shift
    public void Allocate_WhenServiceStartsAtShiftStartOrEndsAtShiftEnd_ChoosesThatTechnician(
        string shiftStart, string shiftEnd, string serviceStart)
    {
        var technician = TechnicianBuilder.Default().OnShift(DayOfWeek.Tuesday, shiftStart, shiftEnd).Build();
        var bay = ServiceBayBuilder.Default().Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, London(Tuesday, serviceStart));

        AssertAllocated(technician, bay, result);
    }

    // EC-23: Riverside is open on Saturday 09:00-13:00, but its only qualified technician does not work Saturdays.
    [Fact]
    public void Allocate_WhenOnlyQualifiedTechnicianHasNoShiftThatWeekday_ReturnsNoQualifiedTechnicianAvailable()
    {
        var technician = TechnicianBuilder.Default().OffShiftOn(DayOfWeek.Saturday).Build();
        var bay = ServiceBayBuilder.Default().Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, London(Saturday, "10:00"));

        AssertFails(BookingErrors.NoQualifiedTechnicianAvailable, result);
    }

    // EC-69: the service is offered (rule 3 counts active + skill only), but a technician with no shifts
    // can never be allocated (ADR-0002 #19).
    [Fact]
    public void Allocate_WhenOnlySkilledTechnicianHasNoShiftsAtAll_ReturnsNoQualifiedTechnicianAvailable()
    {
        var technician = TechnicianBuilder.Default().WithNoShifts().Build();
        var bay = ServiceBayBuilder.Default().Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, TuesdayTen);

        AssertFails(BookingErrors.NoQualifiedTechnicianAvailable, result);
    }

    // EC-05: Standard bays are free, but an EV job needs an EV bay.
    [Fact]
    public void Allocate_WhenEvJobAndOnlyEvBayIsBusy_ReturnsNoCompatibleBayAvailable()
    {
        var evTechnician = TechnicianBuilder.Default().WithSkills(Skills.General, Skills.EV).Build();
        var standardBay = ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build();
        var anotherStandardBay = ServiceBayBuilder.Default().WithId(2).OfType(BayType.Standard).Build();
        var evBay = ServiceBayBuilder.Default().WithId(3).OfType(BayType.EV).Build();
        var busy = BusyIntervalsBuilder.None().BayBusy(evBay, TuesdayTen, 60).Build();

        var result = Allocate(Riverside([evTechnician], [standardBay, anotherStandardBay, evBay]), EvCheck, TuesdayTen, busy);

        AssertFails(BookingErrors.NoCompatibleBayAvailable, result);
    }

    // EC-06: a General-only technician is free, but an EV job needs the EV skill itself.
    [Fact]
    public void Allocate_WhenEvJobAndOnlyEvTechnicianIsBusy_ReturnsNoQualifiedTechnicianAvailable()
    {
        var generalist = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build();
        var evTechnician = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.EV).Build();
        var evBay = ServiceBayBuilder.Default().OfType(BayType.EV).Build();
        var busy = BusyIntervalsBuilder.None().TechnicianBusy(evTechnician, TuesdayTen, 60).Build();

        var result = Allocate(Riverside([generalist, evTechnician], [evBay]), EvCheck, TuesdayTen, busy);

        AssertFails(BookingErrors.NoQualifiedTechnicianAvailable, result);
    }

    // EC-65: when both are missing, the technician error wins (ADR-0002 #17).
    [Fact]
    public void Allocate_WhenNeitherTechnicianNorBayIsFree_ReturnsNoQualifiedTechnicianAvailable()
    {
        var technician = TechnicianBuilder.Default().Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None()
            .TechnicianBusy(technician, TuesdayTen, 60)
            .BayBusy(bay, TuesdayTen, 60)
            .Build();

        var result = Allocate(Riverside([technician], [bay]), OilChange, TuesdayTen, busy);

        AssertFails(BookingErrors.NoQualifiedTechnicianAvailable, result);
    }

    // EC-63: the inactive generalist would win on skills and ID.
    [Fact]
    public void Allocate_WhenBestFitTechnicianIsInactive_ChoosesAnActiveTechnician()
    {
        var inactiveGeneralist = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Inactive().Build();
        var evTechnician = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.EV).Build();
        var bay = ServiceBayBuilder.Default().Build();

        var result = Allocate(Riverside([inactiveGeneralist, evTechnician], [bay]), OilChange, TuesdayTen);

        AssertAllocated(evTechnician, bay, result);
    }

    // EC-63: the inactive Standard bay would win on type and ID.
    [Fact]
    public void Allocate_WhenBestFitBayIsInactive_ChoosesAnActiveBay()
    {
        var technician = TechnicianBuilder.Default().Build();
        var inactiveStandardBay = ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Inactive().Build();
        var liftBay = ServiceBayBuilder.Default().WithId(2).OfType(BayType.Lift).Build();

        var result = Allocate(Riverside([technician], [inactiveStandardBay, liftBay]), OilChange, TuesdayTen);

        AssertAllocated(technician, liftBay, result);
    }

    // EC-07: best fit keeps the EV bay for EV jobs.
    [Fact]
    public void Allocate_WhenStandardAndEvBaysAreFree_ChoosesStandardBay()
    {
        var technician = TechnicianBuilder.Default().Build();
        var evBay = ServiceBayBuilder.Default().WithId(1).OfType(BayType.EV).Build();
        var standardBay = ServiceBayBuilder.Default().WithId(2).OfType(BayType.Standard).Build();

        var result = Allocate(Riverside([technician], [evBay, standardBay]), OilChange, TuesdayTen);

        AssertAllocated(technician, standardBay, result);
    }

    // EC-62: a Standard job can use any bay type (ADR-0002 #9), least specialised first (#18).
    [Fact]
    public void Allocate_WhenStandardJobAndOnlyLiftAndEvBaysExist_ChoosesLiftBay()
    {
        var technician = TechnicianBuilder.Default().Build();
        var evBay = ServiceBayBuilder.Default().WithId(1).OfType(BayType.EV).Build();
        var liftBay = ServiceBayBuilder.Default().WithId(2).OfType(BayType.Lift).Build();

        var result = Allocate(Riverside([technician], [evBay, liftBay]), OilChange, TuesdayTen);

        AssertAllocated(technician, liftBay, result);
    }

    // EC-66: Standard < Lift < Alignment < EV (ADR-0002 #18).
    [Fact]
    public void Allocate_WhenStandardBayIsBusy_ChoosesLiftBeforeAlignmentAndEv()
    {
        var technician = TechnicianBuilder.Default().Build();
        var (evBay, alignmentBay, liftBay, standardBay) = OneBayOfEachType();
        var busy = BusyIntervalsBuilder.None().BayBusy(standardBay, TuesdayTen, 60).Build();

        var result = Allocate(Riverside([technician], [evBay, alignmentBay, liftBay, standardBay]), OilChange, TuesdayTen, busy);

        AssertAllocated(technician, liftBay, result);
    }

    [Fact]
    public void Allocate_WhenStandardAndLiftBaysAreBusy_ChoosesAlignmentBeforeEv()
    {
        var technician = TechnicianBuilder.Default().Build();
        var (evBay, alignmentBay, liftBay, standardBay) = OneBayOfEachType();
        var busy = BusyIntervalsBuilder.None()
            .BayBusy(standardBay, TuesdayTen, 60)
            .BayBusy(liftBay, TuesdayTen, 60)
            .Build();

        var result = Allocate(Riverside([technician], [evBay, alignmentBay, liftBay, standardBay]), OilChange, TuesdayTen, busy);

        AssertAllocated(technician, alignmentBay, result);
    }

    // EC-66: the EV bay is the last resort for a Standard job, but still better than turning the job away.
    [Fact]
    public void Allocate_WhenOnlyEvBayIsFreeForStandardJob_ChoosesEvBay()
    {
        var technician = TechnicianBuilder.Default().Build();
        var (evBay, alignmentBay, liftBay, standardBay) = OneBayOfEachType();
        var busy = BusyIntervalsBuilder.None()
            .BayBusy(standardBay, TuesdayTen, 60)
            .BayBusy(liftBay, TuesdayTen, 60)
            .BayBusy(alignmentBay, TuesdayTen, 60)
            .Build();

        var result = Allocate(Riverside([technician], [evBay, alignmentBay, liftBay, standardBay]), OilChange, TuesdayTen, busy);

        AssertAllocated(technician, evBay, result);
    }

    // EC-67
    [Fact]
    public void Allocate_WhenTwoEquallySpecialisedBaysAreFree_ChoosesLowerBayId()
    {
        var technician = TechnicianBuilder.Default().Build();
        var bayTwo = ServiceBayBuilder.Default().WithId(2).OfType(BayType.Standard).Build();
        var bayOne = ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build();

        var result = Allocate(Riverside([technician], [bayTwo, bayOne]), OilChange, TuesdayTen);

        AssertAllocated(technician, bayOne, result);
    }

    // EC-08: best fit keeps the EV technician for EV jobs.
    [Fact]
    public void Allocate_WhenGeneralistAndEvTechnicianAreFree_ChoosesGeneralist()
    {
        var evTechnician = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General, Skills.EV).Build();
        var generalist = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General).Build();
        var bay = ServiceBayBuilder.Default().Build();

        var result = Allocate(Riverside([evTechnician, generalist], [bay]), OilChange, TuesdayTen);

        AssertAllocated(generalist, bay, result);
    }

    // EC-68: skill count comes before workload.
    [Fact]
    public void Allocate_WhenGeneralistHasSixHoursBookedAndEvTechnicianIsIdle_ChoosesGeneralist()
    {
        var evTechnician = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General, Skills.EV).Build();
        var generalist = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General).Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None().TechnicianBusy(generalist, London(Tuesday, "12:00"), 360).Build();

        var result = Allocate(Riverside([evTechnician, generalist], [bay]), OilChange, TuesdayTen, busy);

        AssertAllocated(generalist, bay, result);
    }

    // EC-29: equal skill counts, so the technician with fewer blocked minutes that day wins.
    [Fact]
    public void Allocate_WhenSkillCountsAreEqual_ChoosesTechnicianWithFewestBookedMinutesThatDay()
    {
        var busierTechnician = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build();
        var quieterTechnician = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General).Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None()
            .TechnicianBusy(busierTechnician, London(Tuesday, "13:00"), 120)
            .TechnicianBusy(quieterTechnician, London(Tuesday, "15:00"), 60)
            .Build();

        var result = Allocate(Riverside([busierTechnician, quieterTechnician], [bay]), OilChange, TuesdayTen, busy);

        AssertAllocated(quieterTechnician, bay, result);
    }

    // EC-29: every skill counts, whichever it is, so {General, EV} and {General, Alignment} tie on skills.
    [Fact]
    public void Allocate_WhenEvAndAlignmentTechniciansAreFree_ChoosesTheLessBookedOne()
    {
        var alignmentTechnician = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General, Skills.Alignment).Build();
        var evTechnician = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.EV).Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None().TechnicianBusy(alignmentTechnician, London(Tuesday, "14:00"), 60).Build();

        var result = Allocate(Riverside([alignmentTechnician, evTechnician], [bay]), OilChange, TuesdayTen, busy);

        AssertAllocated(evTechnician, bay, result);
    }

    // EC-29: a full tie on skills and workload is broken by the lower technician ID.
    [Fact]
    public void Allocate_WhenSkillsAndWorkloadAreEqual_ChoosesLowerTechnicianId()
    {
        var technicianTwo = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General).Build();
        var technicianOne = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build();
        var bay = ServiceBayBuilder.Default().Build();
        var busy = BusyIntervalsBuilder.None()
            .TechnicianBusy(technicianTwo, London(Tuesday, "14:00"), 60)
            .TechnicianBusy(technicianOne, London(Tuesday, "15:00"), 60)
            .Build();

        var result = Allocate(Riverside([technicianTwo, technicianOne], [bay]), OilChange, TuesdayTen, busy);

        AssertAllocated(technicianOne, bay, result);
    }

    // EC-70: workload is counted on the dealership's local date (ADR-0002 #20). Auckland is UTC+13 in
    // November. The request, Tue 10 Nov 10:00 local, is Mon 9 Nov 21:00 UTC.
    // - Mon 14:00-17:00 local is Mon 01:00-04:00 UTC: same UTC date as the request, another local date.
    // - Tue 14:00-15:00 local is Tue 01:00-02:00 UTC: another UTC date, the request's local date.
    // Counting the UTC date would wrongly make bookedOnUtcDateOnly the busier technician.
    [Fact]
    public void Allocate_InAuckland_CountsOnlyThatLocalWorkingDaysBookingsAsWorkload()
    {
        var bookedOnLocalDate = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build();
        var bookedOnUtcDateOnly = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General).Build();
        var bay = ServiceBayBuilder.Default().Build();
        var dealership = DealershipBuilder.Default()
            .InTimeZone("Pacific/Auckland")
            .WithTechnicians(bookedOnLocalDate, bookedOnUtcDateOnly)
            .WithBays(bay)
            .Build();
        var busy = BusyIntervalsBuilder.None()
            .TechnicianBusy(bookedOnUtcDateOnly, Instant("2026-11-09T14:00:00+13:00"), 180)
            .TechnicianBusy(bookedOnLocalDate, Instant("2026-11-10T14:00:00+13:00"), 60)
            .Build();

        var result = Allocate(dealership, OilChange, Instant("2026-11-10T10:00:00+13:00"), busy);

        AssertAllocated(bookedOnUtcDateOnly, bay, result);
    }

    // EC-71: specialised jobs are bookable (ADR-0002 #9). A generalist and a Standard bay are also free, with
    // lower IDs, so only matching the required skill and bay type leads to the expected choice.
    [Fact]
    public void Allocate_WhenEvJobAndEvTechnicianAndEvBayAreFree_ChoosesThem()
    {
        var generalist = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build();
        var evTechnician = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.EV).Build();
        var standardBay = ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build();
        var evBay = ServiceBayBuilder.Default().WithId(2).OfType(BayType.EV).Build();

        var result = Allocate(Riverside([generalist, evTechnician], [standardBay, evBay]), EvCheck, TuesdayTen);

        AssertAllocated(evTechnician, evBay, result);
    }

    [Fact]
    public void Allocate_WhenAlignmentJobAndAlignmentTechnicianAndAlignmentBayAreFree_ChoosesThem()
    {
        var generalist = TechnicianBuilder.Default().WithId(1).WithSkills(Skills.General).Build();
        var alignmentTechnician = TechnicianBuilder.Default().WithId(2).WithSkills(Skills.General, Skills.Alignment).Build();
        var standardBay = ServiceBayBuilder.Default().WithId(1).OfType(BayType.Standard).Build();
        var alignmentBay = ServiceBayBuilder.Default().WithId(2).OfType(BayType.Alignment).Build();
        var wheelAlignment = ServiceTypeBuilder.WheelAlignment().Build();

        var result = Allocate(Riverside([generalist, alignmentTechnician], [standardBay, alignmentBay]), wheelAlignment, TuesdayTen);

        AssertAllocated(alignmentTechnician, alignmentBay, result);
    }

    private static (ServiceBay Ev, ServiceBay Alignment, ServiceBay Lift, ServiceBay Standard) OneBayOfEachType() =>
        (ServiceBayBuilder.Default().WithId(1).OfType(BayType.EV).Build(),
         ServiceBayBuilder.Default().WithId(2).OfType(BayType.Alignment).Build(),
         ServiceBayBuilder.Default().WithId(3).OfType(BayType.Lift).Build(),
         ServiceBayBuilder.Default().WithId(4).OfType(BayType.Standard).Build());

    private static Dealership Riverside(Technician[] technicians, ServiceBay[] bays) =>
        DealershipBuilder.Default().WithTechnicians(technicians).WithBays(bays).Build();

    private static Result<Allocation> Allocate(Dealership dealership, ServiceType serviceType, DateTimeOffset start) =>
        Allocate(dealership, serviceType, start, BusyIntervals.None);

    private static Result<Allocation> Allocate(Dealership dealership, ServiceType serviceType, DateTimeOffset start, BusyIntervals busy)
    {
        var allocator = new SlotAllocator(new BestFitBayPolicy(), new BestFitTechnicianPolicy());

        return allocator.Allocate(new AllocationRequest(dealership, serviceType, TimeSlot.Create(start, serviceType.Duration), busy));
    }

    private static DateTimeOffset London(DateOnly date, string time) => BookingRequestBuilder.Local(date, time);

    private static DateTimeOffset Instant(string iso) => DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);

    private static void AssertAllocated(Technician technician, ServiceBay bay, Result<Allocation> result)
    {
        Assert.True(result.IsSuccess, $"Rejected with {result.Error?.Code}");
        Assert.Equal(technician.Id, result.Value.TechnicianId);
        Assert.Equal(bay.Id, result.Value.BayId);
    }

    private static void AssertFails<T>(DomainError expected, Result<T> result)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
    }
}
