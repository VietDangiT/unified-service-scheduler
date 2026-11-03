using System.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using ServiceScheduler.Application.Appointments;
using ServiceScheduler.Domain.Common;
using ServiceScheduler.Domain.Customers;
using ServiceScheduler.Domain.Dealerships;
using ServiceScheduler.Domain.Scheduling;
using ServiceScheduler.Domain.ServiceTypes;
using ServiceScheduler.Infrastructure.Persistence;
using ServiceScheduler.IntegrationTests.Fixtures;

namespace ServiceScheduler.IntegrationTests.Persistence;

// Saves that share only one resource, while a holder keeps the rows of the other one, so each test checks both of
// AppointmentRepository's row locks: the held one on every run, the shared one through the race (see each test).
// Through the API every request is allocated the same technician and bay, which either lock alone already queues, so
// these call the repository directly. A class of its own, with its own fixture and database, so the extra technicians
// and bays never change what the other classes allocate.
public sealed class LockOrderRaceTests(SchedulingApiFixture fixture) : IClassFixture<SchedulingApiFixture>, IAsyncLifetime
{
    private const int RacingSaves = 20;
    private const short Tuesday = (short)DayOfWeek.Tuesday;

    // The rows the holder keeps: the lock each save takes before the one under test, so every save waits there.
    private const string HoldTechnicianRowsSql = "SELECT 1 FROM technicians WHERE id = ANY(@ids) FOR NO KEY UPDATE";
    private const string HoldBayRowsSql = "SELECT 1 FROM service_bays WHERE id = ANY(@ids) FOR NO KEY UPDATE";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(20);
    // Below Npgsql's 30 s command timeout, so the blocked saves are still waiting when the wait gives up.
    private static readonly TimeSpan BlockedSavesTimeout = TimeSpan.FromSeconds(10);

    private static readonly DateTime TuesdayTen = new(2026, 11, 3, 10, 0, 0);
    private static readonly DateTime MondayNine = new(2026, 11, 2, 9, 0, 0);

    // The seeded oil change: 60 minutes, General skill, Standard bay.
    private static readonly ServiceType OilChange = new(
        new ServiceTypeId(SeedData.OilChangeServiceTypeId), TimeSpan.FromMinutes(60), new Skill("General"), BayType.Standard);

    // The repository stores only IDs, so the seeded dealership carries just its Tuesday hours; each test picks the
    // technician and bay itself instead of asking the allocator.
    private static readonly Dealership SeededDealership = new(
        new DealershipId(SeedData.DealershipId),
        new WeeklySchedule(new Dictionary<DayOfWeek, TimeRange> { [DayOfWeek.Tuesday] = new(new TimeOnly(8, 0), new TimeOnly(17, 0)) }),
        [],
        []);

    // Added by the test setup, not the migration seed. A range of their own, apart from BookingRaceTests (0001) and
    // BayRaceTests (0002).
    private static readonly Guid[] RacingVehicleIds =
        [.. Enumerable.Range(1, RacingSaves).Select(number => new Guid($"ca700000-0000-0000-0003-{number:D12}"))];

    private static readonly Guid[] AddedTechnicianIds =
        [.. Enumerable.Range(1, RacingSaves - 1).Select(number => new Guid($"7ec40000-0000-0000-0003-{number:D12}"))];

    private static readonly Guid[] AddedBayIds =
        [.. Enumerable.Range(1, RacingSaves - 1).Select(number => new Guid($"ba900000-0000-0000-0003-{number:D12}"))];

    private static readonly Guid[] RacingTechnicianIds = [SeedData.TechnicianId, .. AddedTechnicianIds];
    private static readonly Guid[] RacingBayIds = [SeedData.ServiceBayId, .. AddedBayIds];

    public async Task InitializeAsync()
    {
        await fixture.DeleteAppointmentsAsync();

        for (int index = 0; index < RacingVehicleIds.Length; index++)
        {
            await fixture.AddVehicleAsync(RacingVehicleIds[index], $"LOCKRACE{index + 1:D9}");
        }

        await AddGeneralTechniciansAsync();
        await AddStandardBaysAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // EC-77: different technicians, the same bay. The holder keeps the technicians' rows, so every save must wait there,
    // and then lets them all go at once so their inserts land together. It catches a missing technician lock on every
    // run: a save's foreign-key check (KEY SHARE) does not wait for the held NO KEY UPDATE, so it finishes while the rows
    // are held. It catches a missing bay lock only about half the time: the released inserts then meet in the bay's
    // exclusion check and PostgreSQL aborts them with 40P01 instead of a clean 23P01, when they collide.
    [Fact]
    public async Task AddAsync_WhenTwentySavesOnDifferentTechniciansRaceForTheSameBay_ReturnsOneSavedAndNoCompatibleBayForTheRestAndStoresOneAppointment()
    {
        Appointment[] appointments =
            [.. Enumerable.Range(0, RacingSaves).Select(index => BookAtTuesdayTen(RacingVehicleIds[index], RacingTechnicianIds[index], SeedData.ServiceBayId))];

        Task<Result<Appointment>>[] saves =
            await SaveTogetherOnceHeldRowsAreReleasedAsync(appointments, HoldTechnicianRowsSql, RacingTechnicianIds, "technician");

        Assert.All(saves, save => Assert.True(save.IsCompletedSuccessfully, $"The save threw: {save.Exception}"));
        Result<Appointment>[] results = await Task.WhenAll(saves);
        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => !result.IsSuccess), result => Assert.Equal(BookingErrors.NoCompatibleBayAvailable, result.Error));
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // EC-78, the mirror: the same technician, different bays. The holder keeps the bays' rows; with both locks one save
    // holds the technician and waits on its bay while the others wait on the technician. It catches a missing bay lock
    // on every run: the save holding the technician does not wait on its held bay and finishes while the rows are held.
    // It catches a missing technician lock only about half the time: all saves wait on their own bay, and when the
    // holder lets go their inserts meet in the technician's exclusion check (40P01), when they collide.
    [Fact]
    public async Task AddAsync_WhenTwentySavesOnDifferentBaysRaceForTheSameTechnician_ReturnsOneSavedAndNoQualifiedTechnicianForTheRestAndStoresOneAppointment()
    {
        Appointment[] appointments =
            [.. Enumerable.Range(0, RacingSaves).Select(index => BookAtTuesdayTen(RacingVehicleIds[index], SeedData.TechnicianId, RacingBayIds[index]))];

        Task<Result<Appointment>>[] saves =
            await SaveTogetherOnceHeldRowsAreReleasedAsync(appointments, HoldBayRowsSql, RacingBayIds, "bay");

        Assert.All(saves, save => Assert.True(save.IsCompletedSuccessfully, $"The save threw: {save.Exception}"));
        Result<Appointment>[] results = await Task.WhenAll(saves);
        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => !result.IsSuccess), result => Assert.Equal(BookingErrors.NoQualifiedTechnicianAvailable, result.Error));
        Assert.Equal(1, await fixture.CountAppointmentsAsync());
    }

    // Built by the real domain factory, as the booking handler does, with the technician and bay chosen by the test.
    private static Appointment BookAtTuesdayTen(Guid vehicleId, Guid technicianId, Guid bayId)
    {
        var customerId = new CustomerId(SeedData.CustomerId);
        var request = new BookingRequest(customerId, new Vehicle(new VehicleId(vehicleId), customerId), SeededDealership, TuesdayTen, OilChange);
        var allocation = new Allocation(new ServiceBayId(bayId), new TechnicianId(technicianId), TimeSlot.Create(TuesdayTen, OilChange.Duration));

        return Appointment.Book(new AppointmentId(Guid.CreateVersion7()), request, allocation, MondayNine);
    }

    // Each save has its own scope, so its own DbContext and connection, as concurrent requests would. The scope is
    // created before the gate, so the saves are released together rather than as each scope is built.
    private async Task<Result<Appointment>> SaveWhenReleasedAsync(Task gate, Appointment appointment)
    {
        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();
        IAppointmentRepository repository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();

        await gate;

        return await repository.AddAsync(appointment, CancellationToken.None);
    }

    // A holder transaction locks the given rows, the saves start (gate as before) and block on those rows, and only when
    // all of them are blocked does the holder commit, so PostgreSQL wakes every save at the same moment. On a failed
    // run the holder still lets go and every save finishes here, so none leaks into the next test.
    private async Task<Task<Result<Appointment>>[]> SaveTogetherOnceHeldRowsAreReleasedAsync(
        Appointment[] appointments, string holdRowsSql, Guid[] heldRowIds, string heldResource)
    {
        await using var holder = new NpgsqlConnection(fixture.ConnectionString);
        await holder.OpenAsync();
        await using NpgsqlTransaction heldRows = await holder.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand(holdRowsSql, holder, heldRows))
        {
            hold.Parameters.AddWithValue("ids", heldRowIds);
            await hold.ExecuteNonQueryAsync();
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Result<Appointment>>[] saves = [.. appointments.Select(appointment => SaveWhenReleasedAsync(gate.Task, appointment))];
        gate.SetResult();

        try
        {
            await WaitUntilEverySaveIsBlockedOnALockAsync(saves, holder.ProcessID, heldResource);
            await heldRows.CommitAsync();
        }
        finally
        {
            // Rolls back when the commit was not reached (a failed run) and does nothing after a commit, so the holder
            // always lets go before the saves are awaited.
            await heldRows.DisposeAsync();
            await WhenAllCompletedAsync(saves);
        }

        return saves;
    }

    // Polls pg_stat_activity from its own connection: inside a transaction PostgreSQL keeps one snapshot of it. Only the
    // saves can be waiting on a lock in this database, so the holder and this observer are excluded by pid.
    private async Task WaitUntilEverySaveIsBlockedOnALockAsync(Task[] saves, int holderProcessId, string heldResource)
    {
        await using var observer = new NpgsqlConnection(fixture.ConnectionString);
        await observer.OpenAsync();
        await using var countBlocked = new NpgsqlCommand(
            """
            SELECT count(*) FROM pg_stat_activity
            WHERE datname = current_database()
              AND backend_type = 'client backend'
              AND pid NOT IN (@holderPid, pg_backend_pid())
              AND wait_event_type = 'Lock'
            """,
            observer);
        countBlocked.Parameters.AddWithValue("holderPid", holderProcessId);

        var elapsed = Stopwatch.StartNew();
        long blocked = 0;
        while (elapsed.Elapsed < BlockedSavesTimeout)
        {
            // While the rows are held no save that takes the held lock can finish: its foreign-key check (KEY SHARE)
            // does not wait for NO KEY UPDATE, so only the explicit lock makes it wait.
            if (saves.FirstOrDefault(save => save.IsCompleted) is { } finished)
            {
                Assert.Fail(
                    $"A save finished while the {heldResource} rows were held: it did not wait on the held row, so it did not " +
                    $"take the {heldResource} lock. {finished.Exception?.ToString() ?? "It returned a result."}");
            }

            blocked = (long)(await countBlocked.ExecuteScalarAsync() ?? 0L);
            if (blocked == saves.Length)
            {
                return;
            }

            // Waiting on a condition (every save blocked), not a timed sleep as synchronisation: this is the poll interval.
            await Task.Delay(PollInterval);
        }

        Assert.Fail($"Only {blocked} of {saves.Length} saves were waiting on a lock after {BlockedSavesTimeout.TotalSeconds} s.");
    }

    // Waits for every save without throwing on the first fault, so the test can report each save that threw.
    private static async Task WhenAllCompletedAsync(Task[] saves) =>
        await Task.WhenAll(saves).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);

    // Idempotent, because the fixture's database outlives each test and only appointments are reset. Valid rows (General
    // skill, Tuesday shift) although the repository checks neither.
    private async Task AddGeneralTechniciansAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        for (int index = 0; index < AddedTechnicianIds.Length; index++)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO technicians (id, dealership_id, name, is_active)
                VALUES (@id, @dealershipId, @name, true)
                ON CONFLICT (id) DO NOTHING;

                INSERT INTO technician_skills (technician_id, skill)
                VALUES (@id, 'General')
                ON CONFLICT (technician_id, skill) DO NOTHING;

                INSERT INTO technician_shifts (technician_id, day_of_week, starts_at, ends_at)
                VALUES (@id, @tuesday, '08:00', '17:00')
                ON CONFLICT (technician_id, day_of_week) DO NOTHING;
                """,
                connection);
            command.Parameters.AddWithValue("id", AddedTechnicianIds[index]);
            command.Parameters.AddWithValue("dealershipId", SeedData.DealershipId);
            command.Parameters.AddWithValue("name", $"LockRace-Technician-{index + 1:D2}");
            command.Parameters.AddWithValue("tuesday", Tuesday);

            await command.ExecuteNonQueryAsync();
        }
    }

    // Idempotent, for the same reason. Active Standard bays, which the oil change can use.
    private async Task AddStandardBaysAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        for (int index = 0; index < AddedBayIds.Length; index++)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO service_bays (id, dealership_id, name, bay_type, is_active)
                VALUES (@id, @dealershipId, @name, 'Standard', true)
                ON CONFLICT (id) DO NOTHING
                """,
                connection);
            command.Parameters.AddWithValue("id", AddedBayIds[index]);
            command.Parameters.AddWithValue("dealershipId", SeedData.DealershipId);
            command.Parameters.AddWithValue("name", $"LockRace-Bay-{index + 1:D2}");

            await command.ExecuteNonQueryAsync();
        }
    }
}
