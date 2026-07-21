using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualSettlementProcessingAndPayoutScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutDay",
                table: "Vendor",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monday");

            migrationBuilder.AddColumn<string>(
                name: "PayoutDay",
                table: "Drivers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monday");

            // Retire the legacy per-order payout lifecycle while preserving every
            // vendor's existing account. The new columns above default all existing
            // beneficiaries to Monday; only the disabled lifecycle is normalized.
            migrationBuilder.Sql("""
                UPDATE [Vendor]
                SET [FinancialLifecycleMode] = N'Weekly',
                    [PayoutCycle] = N'weekly',
                    [PayoutDay] = N'Monday'
                WHERE [FinancialLifecycleMode] = N'PerOrderDirectPayout'
                   OR LOWER(LTRIM(RTRIM(COALESCE([PayoutCycle], N'')))) IN
                      (N'per_order_direct_payout', N'perorderdirectpayout', N'per-order-direct-payout', N'order_by_order', N'orderbyorder');
                """);

            migrationBuilder.CreateTable(
                name: "PayoutManualConfirmations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProofUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutManualConfirmations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutManualConfirmations_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SettlementProcessingModeAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NewMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementProcessingModeAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SettlementProcessingSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Automatic"),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementProcessingSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutManualConfirmations_ConfirmedBy_ConfirmedAt",
                table: "PayoutManualConfirmations",
                columns: new[] { "ConfirmedByUserId", "ConfirmedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutManualConfirmations_PayoutId",
                table: "PayoutManualConfirmations",
                column: "PayoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementProcessingModeAudits_ChangedAtUtc",
                table: "SettlementProcessingModeAudits",
                column: "ChangedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutManualConfirmations");

            migrationBuilder.DropTable(
                name: "SettlementProcessingModeAudits");

            migrationBuilder.DropTable(
                name: "SettlementProcessingSettings");

            migrationBuilder.DropColumn(
                name: "PayoutDay",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "PayoutDay",
                table: "Drivers");
        }
    }
}
