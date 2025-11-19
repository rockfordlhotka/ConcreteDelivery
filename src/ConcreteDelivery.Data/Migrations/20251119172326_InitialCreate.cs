using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ConcreteDelivery.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trucks",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    driver_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trucks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plant_inventory",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plant_id = table.Column<int>(type: "integer", nullable: false),
                    sand_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    gravel_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    concrete_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_inventory", x => x.id);
                    table.ForeignKey(
                        name: "FK_plant_inventory_plants_plant_id",
                        column: x => x.plant_id,
                        principalTable: "plants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    distance_miles = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    plant_id = table.Column<int>(type: "integer", nullable: true),
                    truck_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_orders_plants_plant_id",
                        column: x => x.plant_id,
                        principalTable: "plants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_orders_trucks_truck_id",
                        column: x => x.truck_id,
                        principalTable: "trucks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "truck_status",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    truck_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    current_order_id = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_truck_status", x => x.id);
                    table.ForeignKey(
                        name: "FK_truck_status_orders_current_order_id",
                        column: x => x.current_order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_truck_status_trucks_truck_id",
                        column: x => x.truck_id,
                        principalTable: "trucks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "plants",
                columns: new[] { "id", "created_at", "name" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "North Plant" },
                    { 2, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "South Plant" },
                    { 3, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "East Plant" }
                });

            migrationBuilder.InsertData(
                table: "trucks",
                columns: new[] { "id", "created_at", "driver_name" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "John Smith" },
                    { 2, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "Maria Garcia" },
                    { 3, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "David Chen" },
                    { 4, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "Sarah Johnson" },
                    { 5, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643), "Michael Brown" }
                });

            migrationBuilder.InsertData(
                table: "plant_inventory",
                columns: new[] { "id", "concrete_quantity", "gravel_quantity", "plant_id", "sand_quantity", "updated_at" },
                values: new object[,]
                {
                    { 1, 500, 800, 1, 1000, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) },
                    { 2, 600, 900, 2, 1200, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) },
                    { 3, 400, 700, 3, 800, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) }
                });

            migrationBuilder.InsertData(
                table: "truck_status",
                columns: new[] { "id", "current_order_id", "status", "truck_id", "updated_at" },
                values: new object[,]
                {
                    { 1, null, "Available", 1, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) },
                    { 2, null, "Available", 2, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) },
                    { 3, null, "Available", 3, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) },
                    { 4, null, "Available", 4, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) },
                    { 5, null, "Available", 5, new DateTime(2025, 11, 19, 17, 23, 26, 226, DateTimeKind.Utc).AddTicks(2643) }
                });

            migrationBuilder.CreateIndex(
                name: "idx_orders_plant_id",
                table: "orders",
                column: "plant_id");

            migrationBuilder.CreateIndex(
                name: "idx_orders_status",
                table: "orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_orders_truck_id",
                table: "orders",
                column: "truck_id");

            migrationBuilder.CreateIndex(
                name: "idx_plant_inventory_plant_id",
                table: "plant_inventory",
                column: "plant_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_truck_status_status",
                table: "truck_status",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_truck_status_truck_id",
                table: "truck_status",
                column: "truck_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_truck_status_current_order_id",
                table: "truck_status",
                column: "current_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plant_inventory");

            migrationBuilder.DropTable(
                name: "truck_status");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "plants");

            migrationBuilder.DropTable(
                name: "trucks");
        }
    }
}
