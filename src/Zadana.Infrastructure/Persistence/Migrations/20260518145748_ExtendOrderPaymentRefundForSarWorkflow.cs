using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendOrderPaymentRefundForSarWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "Refunds",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CompensationMethod",
                table: "Refunds",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "SameMethod");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Refunds",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR");

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAtUtc",
                table: "Refunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleStatus",
                table: "Refunds",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Requested");

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "Refunds",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRefundId",
                table: "Refunds",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawProviderResponse",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RequestedAmount",
                table: "Refunds",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "SucceededAtUtc",
                table: "Refunds",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Payments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Payments",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderInvoiceId",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderMethod",
                table: "Payments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReferenceNumber",
                table: "Payments",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                table: "Payments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawCreateResponse",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawFetchResponse",
                table: "Payments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommissionPolicySnapshot",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Orders",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR");

            migrationBuilder.AddColumn<decimal>(
                name: "DriverCommissionAmount",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PricingMode",
                table: "Orders",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "live");

            migrationBuilder.AddColumn<decimal>(
                name: "ProductGross",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProductNet",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "TaxPolicySnapshot",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VendorCommissionAmount",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Backfill financial-snapshot columns for orders created before the
            // SAR-only revision so the new revenue-distribution formula has
            // consistent inputs to read for historical rows.
            migrationBuilder.Sql(@"
                UPDATE [Orders] SET
                    [ProductGross]            = [Subtotal],
                    [ProductNet]              = CASE WHEN ([Subtotal] - [DiscountTotal]) < 0 THEN 0 ELSE ([Subtotal] - [DiscountTotal]) END,
                    [VendorCommissionAmount]  = [CommissionAmount]
                WHERE [ProductGross] = 0 AND [ProductNet] = 0 AND [VendorCommissionAmount] = 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_Provider_RefundId",
                table: "Refunds",
                columns: new[] { "ProviderName", "ProviderRefundId" },
                filter: "[ProviderRefundId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_Transaction",
                table: "Payments",
                columns: new[] { "ProviderName", "ProviderTransactionId" },
                filter: "[ProviderTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_Provider_RefundId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Payments_IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Provider_Transaction",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "CompensationMethod",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ProviderRefundId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "RawProviderResponse",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "RequestedAmount",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "SucceededAtUtc",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderInvoiceId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderMethod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderReferenceNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RawCreateResponse",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RawFetchResponse",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CommissionPolicySnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DriverCommissionAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PricingMode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProductGross",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProductNet",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxPolicySnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VendorCommissionAmount",
                table: "Orders");
        }
    }
}
