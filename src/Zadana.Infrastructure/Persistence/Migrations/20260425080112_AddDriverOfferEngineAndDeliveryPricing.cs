using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverOfferEngineAndDeliveryPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BaseDeliveryFee",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPricingMode",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPricingRuleLabel",
                table: "Orders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceDeliveryFee",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedDistanceKm",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SurgeDeliveryFee",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DispatchAttemptNumber",
                table: "DeliveryAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferExpiresAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferRejectedAtUtc",
                table: "DeliveryAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferRejectedReason",
                table: "DeliveryAssignments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseDeliveryFee",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPricingMode",
                table: "Carts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPricingRuleLabel",
                table: "Carts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DistanceDeliveryFee",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedDistanceKm",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SurgeDeliveryFee",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "DeliveryOfferAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OfferedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryOfferAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryPricingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    BaseFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedKm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PerKmFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPricingRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryPricingRules_DeliveryZones_DeliveryZoneId",
                        column: x => x.DeliveryZoneId,
                        principalTable: "DeliveryZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryPricingSurgeWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryPricingRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartLocalTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndLocalTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Multiplier = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPricingSurgeWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryPricingSurgeWindows_DeliveryPricingRules_DeliveryPricingRuleId",
                        column: x => x.DeliveryPricingRuleId,
                        principalTable: "DeliveryPricingRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOfferAttempts_OrderId_AttemptNumber",
                table: "DeliveryOfferAttempts",
                columns: new[] { "OrderId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOfferAttempts_OrderId_DriverId_Status",
                table: "DeliveryOfferAttempts",
                columns: new[] { "OrderId", "DriverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPricingRules_City_IsActive",
                table: "DeliveryPricingRules",
                columns: new[] { "City", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPricingRules_DeliveryZoneId_IsActive",
                table: "DeliveryPricingRules",
                columns: new[] { "DeliveryZoneId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryPricingSurgeWindows_DeliveryPricingRuleId_IsActive",
                table: "DeliveryPricingSurgeWindows",
                columns: new[] { "DeliveryPricingRuleId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryOfferAttempts");

            migrationBuilder.DropTable(
                name: "DeliveryPricingSurgeWindows");

            migrationBuilder.DropTable(
                name: "DeliveryPricingRules");

            migrationBuilder.DropColumn(
                name: "BaseDeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPricingMode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryPricingRuleLabel",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DistanceDeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "QuotedDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SurgeDeliveryFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DispatchAttemptNumber",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "OfferExpiresAtUtc",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "OfferRejectedAtUtc",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "OfferRejectedReason",
                table: "DeliveryAssignments");

            migrationBuilder.DropColumn(
                name: "BaseDeliveryFee",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DeliveryPricingMode",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DeliveryPricingRuleLabel",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DistanceDeliveryFee",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "QuotedDistanceKm",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "SurgeDeliveryFee",
                table: "Carts");
        }
    }
}
