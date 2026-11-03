using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ServiceScheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NorthgateDemoSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "customers",
                columns: ["id", "email", "full_name", "phone"],
                values: new object[,]
                {
                    { new Guid("c0570000-0002-0000-0000-000000000001"), "emma.clarke@example.com", "Emma Clarke", "+44 20 7946 0101" },
                    { new Guid("c0570000-0002-0000-0000-000000000002"), "liam.patel@example.com", "Liam Patel", "+44 20 7946 0102" },
                    { new Guid("c0570000-0002-0000-0000-000000000003"), "sofia.rossi@example.com", "Sofia Rossi", "+44 20 7946 0103" }
                });

            migrationBuilder.InsertData(
                table: "dealerships",
                columns: ["id", "name"],
                values: new object[] { new Guid("dea10000-0002-0000-0000-000000000001"), "Northgate" });

            migrationBuilder.InsertData(
                table: "service_types",
                columns: ["id", "buffer_minutes", "code", "duration_minutes", "name", "required_bay_type", "required_skill"],
                values: new object[,]
                {
                    { new Guid("5e7c0000-0002-0000-0000-000000000001"), 0, "ev-battery-check", 90, "EV battery check", "EV", "EV" },
                    { new Guid("5e7c0000-0002-0000-0000-000000000002"), 0, "wheel-alignment", 60, "Wheel alignment", "Alignment", "Alignment" }
                });

            migrationBuilder.InsertData(
                table: "opening_hours",
                columns: ["day_of_week", "dealership_id", "closes_at", "opens_at"],
                values: new object[,]
                {
                    { (short)1, new Guid("dea10000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)2, new Guid("dea10000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)3, new Guid("dea10000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)4, new Guid("dea10000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)5, new Guid("dea10000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) }
                });

            migrationBuilder.InsertData(
                table: "service_bays",
                columns: ["id", "bay_type", "dealership_id", "is_active", "name"],
                values: new object[,]
                {
                    { new Guid("ba900000-0002-0000-0000-000000000001"), "Standard", new Guid("dea10000-0002-0000-0000-000000000001"), true, "Bay-A" },
                    { new Guid("ba900000-0002-0000-0000-000000000002"), "Standard", new Guid("dea10000-0002-0000-0000-000000000001"), true, "Bay-B" },
                    { new Guid("ba900000-0002-0000-0000-000000000003"), "EV", new Guid("dea10000-0002-0000-0000-000000000001"), true, "Bay-EV" },
                    { new Guid("ba900000-0002-0000-0000-000000000004"), "Alignment", new Guid("dea10000-0002-0000-0000-000000000001"), true, "Bay-Align" }
                });

            migrationBuilder.InsertData(
                table: "technicians",
                columns: ["id", "dealership_id", "is_active", "name"],
                values: new object[,]
                {
                    { new Guid("7ec40000-0002-0000-0000-000000000001"), new Guid("dea10000-0002-0000-0000-000000000001"), true, "Ben" },
                    { new Guid("7ec40000-0002-0000-0000-000000000002"), new Guid("dea10000-0002-0000-0000-000000000001"), true, "Chloe" },
                    { new Guid("7ec40000-0002-0000-0000-000000000003"), new Guid("dea10000-0002-0000-0000-000000000001"), true, "Dan" }
                });

            migrationBuilder.InsertData(
                table: "vehicles",
                columns: ["id", "customer_id", "make", "model", "model_year", "vin"],
                values: new object[,]
                {
                    { new Guid("ca700000-0002-0000-0000-000000000001"), new Guid("c0570000-0002-0000-0000-000000000001"), "Ford", "Focus", (short)2020, "WF0NXXGCHNLA12345" },
                    { new Guid("ca700000-0002-0000-0000-000000000002"), new Guid("c0570000-0002-0000-0000-000000000002"), "Toyota", "Corolla", (short)2022, "SB1K93BE10E123456" },
                    { new Guid("ca700000-0002-0000-0000-000000000003"), new Guid("c0570000-0002-0000-0000-000000000003"), "Volkswagen", "Golf", (short)2019, "WVWZZZAUZKW123456" },
                    { new Guid("ca700000-0002-0000-0000-000000000004"), new Guid("c0570000-0002-0000-0000-000000000003"), "Nissan", "Leaf", (short)2023, "SJNFAAZE1U0123456" }
                });

            migrationBuilder.InsertData(
                table: "technician_shifts",
                columns: ["day_of_week", "technician_id", "ends_at", "starts_at"],
                values: new object[,]
                {
                    { (short)1, new Guid("7ec40000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)2, new Guid("7ec40000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)3, new Guid("7ec40000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)4, new Guid("7ec40000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)5, new Guid("7ec40000-0002-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)1, new Guid("7ec40000-0002-0000-0000-000000000002"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)2, new Guid("7ec40000-0002-0000-0000-000000000002"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)3, new Guid("7ec40000-0002-0000-0000-000000000002"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)4, new Guid("7ec40000-0002-0000-0000-000000000002"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)5, new Guid("7ec40000-0002-0000-0000-000000000002"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { (short)1, new Guid("7ec40000-0002-0000-0000-000000000003"), new TimeOnly(17, 0, 0), new TimeOnly(12, 0, 0) },
                    { (short)2, new Guid("7ec40000-0002-0000-0000-000000000003"), new TimeOnly(17, 0, 0), new TimeOnly(12, 0, 0) },
                    { (short)3, new Guid("7ec40000-0002-0000-0000-000000000003"), new TimeOnly(17, 0, 0), new TimeOnly(12, 0, 0) },
                    { (short)4, new Guid("7ec40000-0002-0000-0000-000000000003"), new TimeOnly(17, 0, 0), new TimeOnly(12, 0, 0) },
                    { (short)5, new Guid("7ec40000-0002-0000-0000-000000000003"), new TimeOnly(17, 0, 0), new TimeOnly(12, 0, 0) }
                });

            migrationBuilder.InsertData(
                table: "technician_skills",
                columns: ["skill", "technician_id"],
                values: new object[,]
                {
                    { "General", new Guid("7ec40000-0002-0000-0000-000000000001") },
                    { "EV", new Guid("7ec40000-0002-0000-0000-000000000002") },
                    { "General", new Guid("7ec40000-0002-0000-0000-000000000002") },
                    { "Alignment", new Guid("7ec40000-0002-0000-0000-000000000003") },
                    { "General", new Guid("7ec40000-0002-0000-0000-000000000003") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "opening_hours",
                keyColumns: ["day_of_week", "dealership_id"],
                keyValues: new object[] { (short)1, new Guid("dea10000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "opening_hours",
                keyColumns: ["day_of_week", "dealership_id"],
                keyValues: new object[] { (short)2, new Guid("dea10000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "opening_hours",
                keyColumns: ["day_of_week", "dealership_id"],
                keyValues: new object[] { (short)3, new Guid("dea10000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "opening_hours",
                keyColumns: ["day_of_week", "dealership_id"],
                keyValues: new object[] { (short)4, new Guid("dea10000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "opening_hours",
                keyColumns: ["day_of_week", "dealership_id"],
                keyValues: new object[] { (short)5, new Guid("dea10000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "service_bays",
                keyColumn: "id",
                keyValue: new Guid("ba900000-0002-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "service_bays",
                keyColumn: "id",
                keyValue: new Guid("ba900000-0002-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "service_bays",
                keyColumn: "id",
                keyValue: new Guid("ba900000-0002-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "service_bays",
                keyColumn: "id",
                keyValue: new Guid("ba900000-0002-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "service_types",
                keyColumn: "id",
                keyValue: new Guid("5e7c0000-0002-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "service_types",
                keyColumn: "id",
                keyValue: new Guid("5e7c0000-0002-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)1, new Guid("7ec40000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)2, new Guid("7ec40000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)3, new Guid("7ec40000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)4, new Guid("7ec40000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)5, new Guid("7ec40000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)1, new Guid("7ec40000-0002-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)2, new Guid("7ec40000-0002-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)3, new Guid("7ec40000-0002-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)4, new Guid("7ec40000-0002-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)5, new Guid("7ec40000-0002-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)1, new Guid("7ec40000-0002-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)2, new Guid("7ec40000-0002-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)3, new Guid("7ec40000-0002-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)4, new Guid("7ec40000-0002-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "technician_shifts",
                keyColumns: ["day_of_week", "technician_id"],
                keyValues: new object[] { (short)5, new Guid("7ec40000-0002-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "technician_skills",
                keyColumns: ["skill", "technician_id"],
                keyValues: new object[] { "General", new Guid("7ec40000-0002-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                table: "technician_skills",
                keyColumns: ["skill", "technician_id"],
                keyValues: new object[] { "EV", new Guid("7ec40000-0002-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "technician_skills",
                keyColumns: ["skill", "technician_id"],
                keyValues: new object[] { "General", new Guid("7ec40000-0002-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                table: "technician_skills",
                keyColumns: ["skill", "technician_id"],
                keyValues: new object[] { "Alignment", new Guid("7ec40000-0002-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "technician_skills",
                keyColumns: ["skill", "technician_id"],
                keyValues: new object[] { "General", new Guid("7ec40000-0002-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                table: "vehicles",
                keyColumn: "id",
                keyValue: new Guid("ca700000-0002-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "vehicles",
                keyColumn: "id",
                keyValue: new Guid("ca700000-0002-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "vehicles",
                keyColumn: "id",
                keyValue: new Guid("ca700000-0002-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "vehicles",
                keyColumn: "id",
                keyValue: new Guid("ca700000-0002-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "customers",
                keyColumn: "id",
                keyValue: new Guid("c0570000-0002-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "customers",
                keyColumn: "id",
                keyValue: new Guid("c0570000-0002-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "customers",
                keyColumn: "id",
                keyValue: new Guid("c0570000-0002-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "technicians",
                keyColumn: "id",
                keyValue: new Guid("7ec40000-0002-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "technicians",
                keyColumn: "id",
                keyValue: new Guid("7ec40000-0002-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "technicians",
                keyColumn: "id",
                keyValue: new Guid("7ec40000-0002-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "dealerships",
                keyColumn: "id",
                keyValue: new Guid("dea10000-0002-0000-0000-000000000001"));
        }
    }
}
