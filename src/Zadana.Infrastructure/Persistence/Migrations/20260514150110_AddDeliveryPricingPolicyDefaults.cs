using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryPricingPolicyDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualAssignedDriverPickupDistanceKm",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualDispatchDeviationPercent",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryQuoteLockedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryQuoteStatus",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryQuoteVersion",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverToVendorDistanceKm",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverToVendorFee",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DriverToVendorPricingSource",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDeliveryAnomalyWarning",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PricingOriginDriverId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingOriginType",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsedEstimatedDriverPricing",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VendorToCustomerDistanceKm",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VendorToCustomerFee",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VendorToCustomerPricingSource",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveryQuoteLockedAtUtc",
                table: "Carts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryQuoteStatus",
                table: "Carts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryQuoteVersion",
                table: "Carts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverToVendorDistanceKm",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverToVendorFee",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DriverToVendorPricingSource",
                table: "Carts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasDeliveryAnomalyWarning",
                table: "Carts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PricingOriginDriverId",
                table: "Carts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PricingOriginType",
                table: "Carts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UsedEstimatedDriverPricing",
                table: "Carts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VendorToCustomerDistanceKm",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VendorToCustomerFee",
                table: "Carts",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "VendorToCustomerPricingSource",
                table: "Carts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CityDeliveryPricingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaudiCityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IncludedKm = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExtraKmFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MinDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsPricingActive = table.Column<bool>(type: "bit", nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CodFeeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodFlatFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CodPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsVatActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCodFeeActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CityDeliveryPricingSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryPricingDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedKm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraKmFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsPricingActive = table.Column<bool>(type: "bit", nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodFeeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodFlatFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsVatActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCodFeeActive = table.Column<bool>(type: "bit", nullable: false),
                    MinTotalDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxTotalDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxQuotedDistanceKm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WarningSubtotalRatioThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryPricingDefaults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegionDeliveryPricingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaudiRegionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IncludedKm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ExtraKmFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MinDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxDeliveryFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsPricingActive = table.Column<bool>(type: "bit", nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodFeeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodFlatFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CodPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsVatActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCodFeeActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegionDeliveryPricingSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CityDeliveryPricingSettings_SaudiCityId",
                table: "CityDeliveryPricingSettings",
                column: "SaudiCityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegionDeliveryPricingSettings_SaudiRegionId",
                table: "RegionDeliveryPricingSettings",
                column: "SaudiRegionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CityDeliveryPricingSettings");

            migrationBuilder.DropTable(
                name: "DeliveryPricingDefaults");

            migrationBuilder.DropTable(
                name: "RegionDeliveryPricingSettings");

            migrationBuilder.DropColumn(
                name: "ActualAssignedDriverPickupDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ActualDispatchDeviationPercent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryQuoteLockedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryQuoteStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryQuoteVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DriverToVendorDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DriverToVendorFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DriverToVendorPricingSource",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HasDeliveryAnomalyWarning",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PricingOriginDriverId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PricingOriginType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UsedEstimatedDriverPricing",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VendorToCustomerDistanceKm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VendorToCustomerFee",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VendorToCustomerPricingSource",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryQuoteLockedAtUtc",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DeliveryQuoteStatus",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DeliveryQuoteVersion",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DriverToVendorDistanceKm",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DriverToVendorFee",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "DriverToVendorPricingSource",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "HasDeliveryAnomalyWarning",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "PricingOriginDriverId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "PricingOriginType",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "UsedEstimatedDriverPricing",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "VendorToCustomerDistanceKm",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "VendorToCustomerFee",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "VendorToCustomerPricingSource",
                table: "Carts");
        }
    }
}
