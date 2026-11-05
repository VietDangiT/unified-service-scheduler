using Npgsql;

namespace ServiceScheduler.IntegrationTests.Fixtures;

/// <summary>
/// Inserts a fresh dealership with its own technicians, bays, service types, customers, vehicles and existing
/// appointments, so a test arranges exactly the resources its scenario names and never depends on the migration seed.
/// Every ID is new (<see cref="Guid.CreateVersion7()"/>), so tests sharing one database cannot see each other's resources.
/// </summary>
/// <remarks>
/// Defaults: open Tuesday 08:00-17:00; a 60-minute oil change (<c>General</c> skill, <c>Standard</c> bay, no buffer);
/// one <c>General</c> technician on a Tuesday 08:00-17:00 shift; one active <c>Standard</c> bay; one customer with one
/// vehicle. Technician and bay IDs are handed out in ascending order of declaration, so with equal skills and workload
/// the first one declared is the one the allocator chooses (Allocation policy 1-2).
/// </remarks>
internal sealed class SchedulingDataBuilder
{
    public const string GeneralSkill = "General";
    public const string EvSkill = "EV";

    public const string StandardBay = "Standard";
    public const string LiftBay = "Lift";
    public const string AlignmentBay = "Alignment";
    public const string EvBay = "EV";

    public const string OilChange = "Oil change";

    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    private static readonly TimeOnly Opens = new(8, 0);
    private static readonly TimeOnly Closes = new(17, 0);
    private static readonly WeekdayHours TuesdayHours = new(DayOfWeek.Tuesday, Opens, Closes);

    // Before the fixture's "now" (Monday 2026-11-02 09:00): an existing appointment was booked earlier.
    private static readonly DateTime ExistingAppointmentCreatedAt = new(2026, 11, 1, 12, 0, 0);

    private const int OilChangeMinutes = 60;

    private const string DefaultFullName = "Test Customer";
    private const string DefaultEmail = "test.customer@example.com";
    private const string DefaultPhone = "+00 111 222 333";

    private readonly Dictionary<DayOfWeek, WeekdayHours> _openingHours = new() { [DayOfWeek.Tuesday] = TuesdayHours };
    private readonly List<ServiceTypeSpec> _serviceTypes = [new(OilChange, OilChangeMinutes, GeneralSkill, StandardBay)];
    private readonly List<TechnicianSpec> _technicians = [];
    private readonly List<BaySpec> _bays = [];
    private readonly List<CustomerSpec> _customers = [];
    private readonly List<AppointmentSpec> _appointments = [];

    public SchedulingDataBuilder WithTechnicians(int count, IReadOnlyList<string>? skills = null, bool isActive = true)
    {
        _technicians.AddRange(Enumerable.Repeat(new TechnicianSpec(skills ?? [GeneralSkill], isActive, [TuesdayHours]), count));
        return this;
    }

    /// <summary>One technician; <paramref name="shifts"/> replaces the default Tuesday 08:00-17:00 shift when given.</summary>
    public SchedulingDataBuilder WithTechnician(
        IReadOnlyList<string>? skills = null, bool isActive = true, IReadOnlyList<WeekdayHours>? shifts = null)
    {
        _technicians.Add(new TechnicianSpec(skills ?? [GeneralSkill], isActive, shifts ?? [TuesdayHours]));
        return this;
    }

    public SchedulingDataBuilder WithBays(int count, string bayType = StandardBay, bool isActive = true)
    {
        _bays.AddRange(Enumerable.Repeat(new BaySpec(bayType, isActive), count));
        return this;
    }

    /// <summary>Opens the dealership on that weekday, or replaces its hours there (Tuesday is open by default).</summary>
    public SchedulingDataBuilder WithOpeningHours(DayOfWeek day, TimeOnly opens, TimeOnly closes)
    {
        _openingHours[day] = new WeekdayHours(day, opens, closes);
        return this;
    }

    /// <summary>A service type besides the default oil change; its ID is <see cref="SchedulingData.ServiceTypeIdOf"/>.</summary>
    public SchedulingDataBuilder WithServiceType(
        string name, int durationMinutes, string requiredSkill = GeneralSkill, string requiredBayType = StandardBay)
    {
        _serviceTypes.Add(new ServiceTypeSpec(name, durationMinutes, requiredSkill, requiredBayType));
        return this;
    }

    public SchedulingDataBuilder WithCustomer(
        int vehicles = 1,
        string fullName = DefaultFullName,
        string email = DefaultEmail,
        string phone = DefaultPhone)
    {
        _customers.Add(new CustomerSpec(fullName, email, phone, vehicles));
        return this;
    }

    /// <summary>
    /// An appointment that already exists, written straight to the database (ADR-0002 #28 for a cancelled one). The
    /// indexes are positions in declaration order: the first customer's vehicles, the technicians and the bays. It ends
    /// after the service type's duration, with no buffer.
    /// </summary>
    public SchedulingDataBuilder WithAppointment(
        int vehicle, int technician, int bay, DateTime start, string serviceType = OilChange, string status = Confirmed)
    {
        _appointments.Add(new AppointmentSpec(vehicle, technician, bay, start, serviceType, status));
        return this;
    }

    public async Task<SchedulingData> BuildAsync(string connectionString)
    {
        IReadOnlyList<TechnicianSpec> technicians = _technicians.Count > 0 ? _technicians : [new TechnicianSpec([GeneralSkill], true, [TuesdayHours])];
        IReadOnlyList<BaySpec> bays = _bays.Count > 0 ? _bays : [new BaySpec(StandardBay, true)];
        IReadOnlyList<CustomerSpec> customers = _customers.Count > 0 ? _customers : [new CustomerSpec(DefaultFullName, DefaultEmail, DefaultPhone, 1)];

        var data = new SchedulingData(
            DealershipId: Guid.CreateVersion7(),
            ServiceTypeIds: _serviceTypes.ToDictionary(serviceType => serviceType.Name, _ => Guid.CreateVersion7()),
            TechnicianIds: AscendingIds(technicians.Count),
            BayIds: AscendingIds(bays.Count),
            Customers: [.. customers.Select(customer => new CreatedCustomer(
                Guid.CreateVersion7(), customer.FullName, customer.Email, customer.Phone,
                [.. Enumerable.Range(0, customer.Vehicles).Select(_ => Guid.CreateVersion7())]))]);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();

        await InsertDealershipAsync(connection, transaction, data.DealershipId, _openingHours.Values);

        foreach (ServiceTypeSpec serviceType in _serviceTypes)
        {
            await InsertServiceTypeAsync(connection, transaction, data.ServiceTypeIdOf(serviceType.Name), serviceType);
        }

        for (int index = 0; index < technicians.Count; index++)
        {
            await InsertTechnicianAsync(connection, transaction, data.DealershipId, data.TechnicianIds[index], index + 1, technicians[index]);
        }

        for (int index = 0; index < bays.Count; index++)
        {
            await InsertBayAsync(connection, transaction, data.DealershipId, data.BayIds[index], index + 1, bays[index]);
        }

        foreach (CreatedCustomer customer in data.Customers)
        {
            await InsertCustomerAsync(connection, transaction, customer);
        }

        foreach (AppointmentSpec appointment in _appointments)
        {
            await InsertAppointmentAsync(connection, transaction, data, appointment);
        }

        await transaction.CommitAsync();

        return data;
    }

    /// <summary>The code a service type is stored with: unique across the database because it carries the ID.</summary>
    public static string ServiceTypeCodeOf(Guid serviceTypeId) => $"service-{serviceTypeId:N}";

    // Sorted with the same comparer the allocator's ID tie-break uses: two version 7 IDs made in the same millisecond
    // are not ordered by creation.
    private static Guid[] AscendingIds(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => Guid.CreateVersion7()).Order()];

    private static async Task InsertDealershipAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid dealershipId, IEnumerable<WeekdayHours> openingHours)
    {
        await using (var command = new NpgsqlCommand(
            "INSERT INTO dealerships (id, name) VALUES (@id, 'Test dealership')", connection, transaction))
        {
            command.Parameters.AddWithValue("id", dealershipId);
            await command.ExecuteNonQueryAsync();
        }

        foreach (WeekdayHours hours in openingHours)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO opening_hours (dealership_id, day_of_week, opens_at, closes_at)
                VALUES (@id, @day, @opens, @closes)
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", dealershipId);
            command.Parameters.AddWithValue("day", (short)hours.Day);
            command.Parameters.AddWithValue("opens", hours.From);
            command.Parameters.AddWithValue("closes", hours.To);

            await command.ExecuteNonQueryAsync();
        }
    }

    // The code is unique across the database, so it carries the ID.
    private static async Task InsertServiceTypeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid serviceTypeId, ServiceTypeSpec serviceType)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_types (id, code, name, duration_minutes, buffer_minutes, required_skill, required_bay_type)
            VALUES (@id, @code, @name, @duration, 0, @skill, @bayType)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", serviceTypeId);
        command.Parameters.AddWithValue("code", ServiceTypeCodeOf(serviceTypeId));
        command.Parameters.AddWithValue("name", serviceType.Name);
        command.Parameters.AddWithValue("duration", serviceType.DurationMinutes);
        command.Parameters.AddWithValue("skill", serviceType.RequiredSkill);
        command.Parameters.AddWithValue("bayType", serviceType.RequiredBayType);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertTechnicianAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid dealershipId, Guid technicianId, int number, TechnicianSpec technician)
    {
        await using (var command = new NpgsqlCommand(
            """
            INSERT INTO technicians (id, dealership_id, name, is_active)
            VALUES (@id, @dealershipId, @name, @isActive);

            INSERT INTO technician_skills (technician_id, skill)
            SELECT @id, skill FROM unnest(@skills) AS skill;
            """,
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("id", technicianId);
            command.Parameters.AddWithValue("dealershipId", dealershipId);
            command.Parameters.AddWithValue("name", $"Technician {number}");
            command.Parameters.AddWithValue("isActive", technician.IsActive);
            command.Parameters.AddWithValue("skills", technician.Skills.ToArray());

            await command.ExecuteNonQueryAsync();
        }

        foreach (WeekdayHours shift in technician.Shifts)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO technician_shifts (technician_id, day_of_week, starts_at, ends_at)
                VALUES (@id, @day, @starts, @ends)
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", technicianId);
            command.Parameters.AddWithValue("day", (short)shift.Day);
            command.Parameters.AddWithValue("starts", shift.From);
            command.Parameters.AddWithValue("ends", shift.To);

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertBayAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid dealershipId, Guid bayId, int number, BaySpec bay)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO service_bays (id, dealership_id, name, bay_type, is_active)
            VALUES (@id, @dealershipId, @name, @bayType, @isActive)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", bayId);
        command.Parameters.AddWithValue("dealershipId", dealershipId);
        command.Parameters.AddWithValue("name", $"Bay {number}");
        command.Parameters.AddWithValue("bayType", bay.BayType);
        command.Parameters.AddWithValue("isActive", bay.IsActive);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertCustomerAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CreatedCustomer customer)
    {
        await using (var command = new NpgsqlCommand(
            "INSERT INTO customers (id, full_name, email, phone) VALUES (@id, @fullName, @email, @phone)",
            connection,
            transaction))
        {
            command.Parameters.AddWithValue("id", customer.Id);
            command.Parameters.AddWithValue("fullName", customer.FullName);
            command.Parameters.AddWithValue("email", customer.Email);
            command.Parameters.AddWithValue("phone", customer.Phone);

            await command.ExecuteNonQueryAsync();
        }

        foreach (Guid vehicleId in customer.VehicleIds)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO vehicles (id, customer_id, vin, make, model, model_year)
                VALUES (@id, @customerId, @vin, 'Toyota', 'Corolla', 2022)
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", vehicleId);
            command.Parameters.AddWithValue("customerId", customer.Id);
            command.Parameters.AddWithValue("vin", VinFor(vehicleId));

            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task InsertAppointmentAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, SchedulingData data, AppointmentSpec appointment)
    {
        ServiceTypeSpec serviceType = _serviceTypes.Single(candidate => candidate.Name == appointment.ServiceType);
        DateTime end = appointment.Start.AddMinutes(serviceType.DurationMinutes);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO appointments (id, dealership_id, customer_id, vehicle_id, service_type_id, technician_id,
                                      service_bay_id, start_at, end_at, blocked_until, status, created_at)
            VALUES (@id, @dealershipId, @customerId, @vehicleId, @serviceTypeId, @technicianId,
                    @serviceBayId, @startAt, @endAt, @endAt, @status, @createdAt)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("dealershipId", data.DealershipId);
        command.Parameters.AddWithValue("customerId", data.Customer.Id);
        command.Parameters.AddWithValue("vehicleId", data.Customer.VehicleIds[appointment.Vehicle]);
        command.Parameters.AddWithValue("serviceTypeId", data.ServiceTypeIdOf(appointment.ServiceType));
        command.Parameters.AddWithValue("technicianId", data.TechnicianIds[appointment.Technician]);
        command.Parameters.AddWithValue("serviceBayId", data.BayIds[appointment.Bay]);
        command.Parameters.AddWithValue("startAt", appointment.Start);
        command.Parameters.AddWithValue("endAt", end);
        command.Parameters.AddWithValue("status", appointment.Status);
        command.Parameters.AddWithValue("createdAt", ExistingAppointmentCreatedAt);

        await command.ExecuteNonQueryAsync();
    }

    // The VIN is unique across the database. The last 17 hex digits of a version 7 ID are nearly all random bits, so
    // vehicles made in the same millisecond still get different VINs.
    private static string VinFor(Guid vehicleId) => vehicleId.ToString("N")[^17..].ToUpperInvariant();

    private sealed record TechnicianSpec(IReadOnlyList<string> Skills, bool IsActive, IReadOnlyList<WeekdayHours> Shifts);

    private sealed record BaySpec(string BayType, bool IsActive);

    private sealed record ServiceTypeSpec(string Name, int DurationMinutes, string RequiredSkill, string RequiredBayType);

    private sealed record CustomerSpec(string FullName, string Email, string Phone, int Vehicles);

    private sealed record AppointmentSpec(int Vehicle, int Technician, int Bay, DateTime Start, string ServiceType, string Status);
}

/// <summary>A <c>[from, to)</c> range on one weekday: opening hours or a shift.</summary>
internal sealed record WeekdayHours(DayOfWeek Day, TimeOnly From, TimeOnly To);

/// <summary>The IDs <see cref="SchedulingDataBuilder"/> created. Technician and bay IDs are in ascending order.</summary>
internal sealed record SchedulingData(
    Guid DealershipId,
    IReadOnlyDictionary<string, Guid> ServiceTypeIds,
    IReadOnlyList<Guid> TechnicianIds,
    IReadOnlyList<Guid> BayIds,
    IReadOnlyList<CreatedCustomer> Customers)
{
    /// <summary>The default oil change.</summary>
    public Guid ServiceTypeId => ServiceTypeIdOf(SchedulingDataBuilder.OilChange);

    /// <summary>The code of the default oil change.</summary>
    public string ServiceTypeCode => SchedulingDataBuilder.ServiceTypeCodeOf(ServiceTypeId);

    /// <summary>The first customer: the only one unless the test added more.</summary>
    public CreatedCustomer Customer => Customers[0];

    public Guid ServiceTypeIdOf(string name) => ServiceTypeIds[name];

    public Guid OwnerOf(Guid vehicleId) => Customers.Single(customer => customer.VehicleIds.Contains(vehicleId)).Id;
}

internal sealed record CreatedCustomer(Guid Id, string FullName, string Email, string Phone, IReadOnlyList<Guid> VehicleIds);
