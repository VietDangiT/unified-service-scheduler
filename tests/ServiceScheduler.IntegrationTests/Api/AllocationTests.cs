using System.Net;

using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Api;

// Which free technician and bay a booking gets (Allocation policy 1-2): best fit, so a scarce resource is never spent on
// a job a less specialised one can do. The resource best fit must pass over is declared first, so the ID tie-break
// would choose it if the policy were broken. "Tuesday" is 2026-11-03.
public sealed class AllocationTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    private const string TuesdayTen = "2026-11-03T10:00:00";

    public async Task InitializeAsync() => await fixture.DeleteAppointmentsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Scenario 21, EC-07/62/66: an oil change takes the least specialised bay, in the order Standard < Lift < Alignment
    // < EV (ADR-0002 #18): (a) the Standard bay before Lift and EV; (b) with no Standard bay, Lift before Alignment and EV.
    [Theory]
    [InlineData("EV,Lift,Standard", 2)]
    [InlineData("EV,Alignment,Lift", 2)]
    public async Task PostAppointment_WhenSeveralBayTypesAreFree_ReturnsCreatedOnTheLeastSpecialisedBay(string declaredBayTypes, int expectedBay)
    {
        var builder = new SchedulingDataBuilder().WithTechnicians(1);
        foreach (string bayType in declaredBayTypes.Split(','))
        {
            builder.WithBays(1, bayType);
        }

        SchedulingData data = await builder.BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[0], data.BayIds[expectedBay]), await AppointmentsApi.ReadAllocationAsync(response));
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Scenario 22, EC-08: the EV specialist, declared first, is kept for the jobs only they can do.
    [Fact]
    public async Task PostAppointment_WhenAGeneralistAndAnEvTechnicianAreFree_ReturnsCreatedOnTheGeneralist()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnician(skills: [SchedulingDataBuilder.GeneralSkill, SchedulingDataBuilder.EvSkill])
            .WithTechnician()
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[0], TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[1], data.BayIds[0]), await AppointmentsApi.ReadAllocationAsync(response));
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Scenario 23, EC-29: equal skills, so the less-loaded technician that date wins (ADR-0002 #20). T1, declared first,
    // already has an 08:00-09:00 job, which does not overlap the 10:00 request.
    [Fact]
    public async Task PostAppointment_WhenTechniciansHaveEqualSkills_ReturnsCreatedOnTheOneWithLessWorkThatDate()
    {
        SchedulingData data = await new SchedulingDataBuilder()
            .WithTechnicians(2)
            .WithCustomer(vehicles: 2)
            .WithAppointment(vehicle: 0, technician: 0, bay: 0, new DateTime(2026, 11, 3, 8, 0, 0))
            .BuildAsync(fixture.ConnectionString);
        using HttpClient client = fixture.CreateClient();

        using HttpResponseMessage response = await AppointmentsApi.PostAppointmentAsync(client, data, data.Customer.VehicleIds[1], TuesdayTen);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((data.TechnicianIds[1], data.BayIds[0]), await AppointmentsApi.ReadAllocationAsync(response));
        Assert.Equal(2, await fixture.CountAppointmentsAsync());
    }
}
