using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupFulfillment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerAddressId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedToDeliveryAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryUpgradePaymentId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Fulfillment",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Delivery");

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupNoShowDeadlineUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupOtpCode",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupOtpExpiresAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickupOtpFailedAttempts",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupOtpLockedUntilUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickupOtpResendCount",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupOtpResendWindowStartedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PickupOtpVerifiedAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupOtpVerifiedByVendorUserId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PickupReminder50Sent",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PickupReminder90Sent",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadyForPickupAtUtc",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Orders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "OrderCancellationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomerReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VendorResponseNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderCancellationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderCancellationRequests_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlatformPickupSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryOptionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PickupOptionEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PickupCommissionPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PickupNoShowTimeoutHours = table.Column<int>(type: "int", nullable: false),
                    PickupOtpMaxAttempts = table.Column<int>(type: "int", nullable: false),
                    PickupOtpLockoutMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformPickupSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Fulfillment_Status_NoShowDeadline",
                table: "Orders",
                columns: new[] { "Fulfillment", "Status", "PickupNoShowDeadlineUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Fulfillment_Status_ReadyForPickup",
                table: "Orders",
                columns: new[] { "Fulfillment", "Status", "ReadyForPickupAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderCancellationRequests_OrderId_Status",
                table: "OrderCancellationRequests",
                columns: new[] { "OrderId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderCancellationRequests");

            migrationBuilder.DropTable(
                name: "PlatformPickupSettings");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Fulfillment_Status_NoShowDeadline",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Fulfillment_Status_ReadyForPickup",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConvertedToDeliveryAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryUpgradePaymentId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Fulfillment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupNoShowDeadlineUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpExpiresAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpFailedAttempts",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpLockedUntilUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpResendCount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpResendWindowStartedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpVerifiedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupOtpVerifiedByVendorUserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupReminder50Sent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupReminder90Sent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReadyForPickupAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerAddressId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
