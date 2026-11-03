using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceScheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dealerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dealerships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    buffer_minutes = table.Column<int>(type: "integer", nullable: false),
                    required_skill = table.Column<string>(type: "text", nullable: false),
                    required_bay_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_types", x => x.id);
                    table.UniqueConstraint("uq_service_types_code", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vin = table.Column<string>(type: "text", nullable: false),
                    make = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    model_year = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.UniqueConstraint("uq_vehicles_vin", x => x.vin);
                    table.ForeignKey(
                        name: "fk_vehicles_customer",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opening_hours",
                columns: table => new
                {
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<short>(type: "smallint", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_hours", x => new { x.dealership_id, x.day_of_week });
                    table.ForeignKey(
                        name: "fk_opening_hours_dealership",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_bays",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    bay_type = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_bays", x => x.id);
                    table.UniqueConstraint("uq_service_bays_id_dealership", x => new { x.id, x.dealership_id });
                    table.ForeignKey(
                        name: "fk_service_bays_dealership",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technicians",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technicians", x => x.id);
                    table.UniqueConstraint("uq_technicians_id_dealership", x => new { x.id, x.dealership_id });
                    table.ForeignKey(
                        name: "fk_technicians_dealership",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_bay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    blocked_until = table.Column<DateTime>(type: "timestamp", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointments_bay_same_dealership",
                        columns: x => new { x.service_bay_id, x.dealership_id },
                        principalTable: "service_bays",
                        principalColumns: ["id", "dealership_id"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_customer",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_dealership",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_service_type",
                        column: x => x.service_type_id,
                        principalTable: "service_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_technician_same_dealership",
                        columns: x => new { x.technician_id, x.dealership_id },
                        principalTable: "technicians",
                        principalColumns: ["id", "dealership_id"],
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointments_vehicle",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technician_shifts",
                columns: table => new
                {
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<short>(type: "smallint", nullable: false),
                    starts_at = table.Column<TimeOnly>(type: "time", nullable: false),
                    ends_at = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technician_shifts", x => new { x.technician_id, x.day_of_week });
                    table.ForeignKey(
                        name: "fk_technician_shifts_technician",
                        column: x => x.technician_id,
                        principalTable: "technicians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technician_skills",
                columns: table => new
                {
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technician_skills", x => new { x.technician_id, x.skill });
                    table.ForeignKey(
                        name: "fk_technician_skills_technician",
                        column: x => x.technician_id,
                        principalTable: "technicians",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "customers",
                columns: ["id", "email", "full_name", "phone"],
                values: new object[] { new Guid("c0570000-0000-0000-0000-000000000001"), "customer@example.com", "Sample Customer", "+00 000 000 000" });

            migrationBuilder.InsertData(
                table: "dealerships",
                columns: ["id", "name"],
                values: new object[] { new Guid("dea10000-0000-0000-0000-000000000001"), "Riverside" });

            migrationBuilder.InsertData(
                table: "service_types",
                columns: ["id", "buffer_minutes", "code", "duration_minutes", "name", "required_bay_type", "required_skill"],
                values: new object[] { new Guid("5e7c0000-0000-0000-0000-000000000001"), 0, "oil-change", 60, "Oil change", "Standard", "General" });

            migrationBuilder.InsertData(
                table: "opening_hours",
                columns: ["day_of_week", "dealership_id", "closes_at", "opens_at"],
                values: new object[] { (short)2, new Guid("dea10000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) });

            migrationBuilder.InsertData(
                table: "service_bays",
                columns: ["id", "bay_type", "dealership_id", "is_active", "name"],
                values: new object[] { new Guid("ba900000-0000-0000-0000-000000000001"), "Standard", new Guid("dea10000-0000-0000-0000-000000000001"), true, "Bay-1" });

            migrationBuilder.InsertData(
                table: "technicians",
                columns: ["id", "dealership_id", "is_active", "name"],
                values: new object[] { new Guid("7ec40000-0000-0000-0000-000000000001"), new Guid("dea10000-0000-0000-0000-000000000001"), true, "Alice" });

            migrationBuilder.InsertData(
                table: "vehicles",
                columns: ["id", "customer_id", "make", "model", "model_year", "vin"],
                values: new object[] { new Guid("ca700000-0000-0000-0000-000000000001"), new Guid("c0570000-0000-0000-0000-000000000001"), "Honda", "Civic", (short)2021, "1HGBH41JXMN109186" });

            migrationBuilder.InsertData(
                table: "technician_shifts",
                columns: ["day_of_week", "technician_id", "ends_at", "starts_at"],
                values: new object[] { (short)2, new Guid("7ec40000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) });

            migrationBuilder.InsertData(
                table: "technician_skills",
                columns: ["skill", "technician_id"],
                values: new object[] { "General", new Guid("7ec40000-0000-0000-0000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_dealership_start",
                table: "appointments",
                columns: ["dealership_id", "start_at"],
                filter: "status = 'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "ix_service_bays_dealership_id",
                table: "service_bays",
                column: "dealership_id");

            migrationBuilder.CreateIndex(
                name: "ix_technicians_dealership_id",
                table: "technicians",
                column: "dealership_id");

            // The constraints that carry business rules: docs/system-design.md §6.2. EF Core cannot express an
            // exclusion constraint, and keeping every CHECK beside them leaves one place to read. The range checks
            // refuse 24:00 because PostgreSQL `time` accepts it and TimeOnly cannot read it back.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                """
                ALTER TABLE appointments
                  ADD CONSTRAINT ck_appointments_interval CHECK (end_at > start_at AND blocked_until >= end_at),
                  ADD CONSTRAINT ck_appointments_status   CHECK (status IN ('Confirmed', 'Cancelled'));

                ALTER TABLE opening_hours     ADD CONSTRAINT ck_opening_hours_range     CHECK (closes_at > opens_at AND closes_at < '24:00');
                ALTER TABLE opening_hours     ADD CONSTRAINT ck_opening_hours_day       CHECK (day_of_week BETWEEN 0 AND 6);
                ALTER TABLE technician_shifts ADD CONSTRAINT ck_technician_shifts_range CHECK (ends_at > starts_at AND ends_at < '24:00');
                ALTER TABLE technician_shifts ADD CONSTRAINT ck_technician_shifts_day   CHECK (day_of_week BETWEEN 0 AND 6);

                ALTER TABLE service_bays  ADD CONSTRAINT ck_service_bays_type
                  CHECK (bay_type IN ('Standard', 'Lift', 'Alignment', 'EV'));
                ALTER TABLE service_types ADD CONSTRAINT ck_service_types_required_bay_type
                  CHECK (required_bay_type IN ('Standard', 'Lift', 'Alignment', 'EV'));
                ALTER TABLE service_types ADD CONSTRAINT ck_service_types_minutes
                  CHECK (duration_minutes > 0 AND buffer_minutes >= 0);
                """);

            // No double-booking. '[)' is half-open, so 10:00-11:00 and 11:00-12:00 do not conflict. Technician and
            // bay are blocked until the buffer ends; the vehicle only for the service itself.
            migrationBuilder.Sql(
                """
                ALTER TABLE appointments ADD CONSTRAINT ex_appointments_technician_overlap
                  EXCLUDE USING gist (technician_id WITH =, tsrange(start_at, blocked_until, '[)') WITH &&)
                  WHERE (status = 'Confirmed');

                ALTER TABLE appointments ADD CONSTRAINT ex_appointments_bay_overlap
                  EXCLUDE USING gist (service_bay_id WITH =, tsrange(start_at, blocked_until, '[)') WITH &&)
                  WHERE (status = 'Confirmed');

                ALTER TABLE appointments ADD CONSTRAINT ex_appointments_vehicle_overlap
                  EXCLUDE USING gist (vehicle_id WITH =, tsrange(start_at, end_at, '[)') WITH &&)
                  WHERE (status = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE appointments DROP CONSTRAINT ex_appointments_vehicle_overlap;
                ALTER TABLE appointments DROP CONSTRAINT ex_appointments_bay_overlap;
                ALTER TABLE appointments DROP CONSTRAINT ex_appointments_technician_overlap;

                ALTER TABLE service_types DROP CONSTRAINT ck_service_types_minutes;
                ALTER TABLE service_types DROP CONSTRAINT ck_service_types_required_bay_type;
                ALTER TABLE service_bays DROP CONSTRAINT ck_service_bays_type;
                ALTER TABLE technician_shifts DROP CONSTRAINT ck_technician_shifts_day;
                ALTER TABLE technician_shifts DROP CONSTRAINT ck_technician_shifts_range;
                ALTER TABLE opening_hours DROP CONSTRAINT ck_opening_hours_day;
                ALTER TABLE opening_hours DROP CONSTRAINT ck_opening_hours_range;
                ALTER TABLE appointments DROP CONSTRAINT ck_appointments_status;
                ALTER TABLE appointments DROP CONSTRAINT ck_appointments_interval;
                """);

            migrationBuilder.DropTable(
                name: "appointments");

            migrationBuilder.DropTable(
                name: "opening_hours");

            migrationBuilder.DropTable(
                name: "technician_shifts");

            migrationBuilder.DropTable(
                name: "technician_skills");

            migrationBuilder.DropTable(
                name: "service_bays");

            migrationBuilder.DropTable(
                name: "service_types");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "technicians");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "dealerships");

            migrationBuilder.Sql("DROP EXTENSION IF EXISTS btree_gist;");
        }
    }
}
