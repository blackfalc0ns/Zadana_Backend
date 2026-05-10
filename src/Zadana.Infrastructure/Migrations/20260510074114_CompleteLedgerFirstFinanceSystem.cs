using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteLedgerFirstFinanceSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SettlementItems_SettlementId",
                table: "SettlementItems");

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmount",
                table: "Settlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Settlements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "OwnerType",
                table: "Settlements",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodFrom",
                table: "Settlements",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodTo",
                table: "Settlements",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "RecoveryAmount",
                table: "Settlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundAmount",
                table: "Settlements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionType",
                table: "Settlements",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "SettlementItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<decimal>(
                name: "Adjustment",
                table: "SettlementItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                table: "SettlementItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Commission",
                table: "SettlementItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "LineType",
                table: "SettlementItems",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "NetAmount",
                table: "SettlementItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Recovery",
                table: "SettlementItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Refund",
                table: "SettlementItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "SettlementItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "Payouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationSnapshot",
                table: "Payouts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DestinationType",
                table: "Payouts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Payouts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByUserId",
                table: "Payouts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "Payouts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransferId",
                table: "Payouts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TriggeredAtUtc",
                table: "Payouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayoutAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderTransferId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransferReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutAttempts_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_Owner_Period",
                table: "Settlements",
                columns: new[] { "OwnerType", "OwnerId", "PeriodFrom", "PeriodTo" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementItems_Settlement_Source",
                table: "SettlementItems",
                columns: new[] { "SettlementId", "LineType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payouts_ProviderTransferId",
                table: "Payouts",
                column: "ProviderTransferId",
                unique: true,
                filter: "[ProviderTransferId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutAttempts_Payout_Attempt_ProviderTransfer",
                table: "PayoutAttempts",
                columns: new[] { "PayoutId", "AttemptType", "ProviderTransferId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_Owner_Period",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_SettlementItems_Settlement_Source",
                table: "SettlementItems");

            migrationBuilder.DropIndex(
                name: "IX_Payouts_ProviderTransferId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmount",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "PeriodFrom",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "PeriodTo",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "RecoveryAmount",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "RefundAmount",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "ResolutionType",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "Adjustment",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "Amount",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "Commission",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "LineType",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "NetAmount",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "Recovery",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "Refund",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "SettlementItems");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "DestinationSnapshot",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "DestinationType",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ProviderTransferId",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "TriggeredAtUtc",
                table: "Payouts");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "SettlementItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementItems_SettlementId",
                table: "SettlementItems",
                column: "SettlementId");
        }
    }
}
