using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenPayoutExecutionSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverWithdrawalRequests_PayoutId",
                table: "DriverWithdrawalRequests");

            migrationBuilder.AddColumn<bool>(
                name: "RequireManualPayoutDualControl",
                table: "SettlementProcessingSettings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Payouts",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.CreateTable(
                name: "PayoutExecutionReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaimedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmissionReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutExecutionReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutExecutionReservations_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayoutReversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProofUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutReversals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutReversals_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_DriverWithdrawalRequests_PayoutId",
                table: "DriverWithdrawalRequests",
                column: "PayoutId",
                unique: true,
                filter: "[PayoutId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutExecutionReservations_Mode_Status_ClaimedAt",
                table: "PayoutExecutionReservations",
                columns: new[] { "Mode", "Status", "ClaimedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutExecutionReservations_PayoutId",
                table: "PayoutExecutionReservations",
                column: "PayoutId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutReversals_ConfirmedBy_ConfirmedAt",
                table: "PayoutReversals",
                columns: new[] { "ConfirmedByUserId", "ConfirmedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutReversals_PayoutId",
                table: "PayoutReversals",
                column: "PayoutId",
                unique: true);

            // Preserve the ability to reconcile manual payouts that were
            // already marked Processing/Queued by the legacy workflow before
            // execution reservations existed. New payouts must always go
            // through Claim -> BankSubmission; this backfill only fences old
            // in-flight records so Automatic mode can never submit them.
            migrationBuilder.Sql("""
                INSERT INTO [PayoutExecutionReservations]
                    ([Id], [PayoutId], [Mode], [Status], [ClaimedByUserId], [ClaimedAtUtc],
                     [SubmittedByUserId], [SubmittedAtUtc], [SubmissionReference],
                     [ReleasedByUserId], [ReleasedAtUtc], [ReleaseReason], [CreatedAtUtc], [UpdatedAtUtc])
                SELECT
                    NEWID(),
                    [p].[Id],
                    N'Manual',
                    N'Submitted',
                    NULL,
                    COALESCE([p].[TriggeredAtUtc], [p].[CreatedAtUtc]),
                    NULL,
                    COALESCE([p].[TriggeredAtUtc], [p].[CreatedAtUtc]),
                    N'Legacy manual payout awaiting confirmation',
                    NULL,
                    NULL,
                    NULL,
                    [p].[CreatedAtUtc],
                    [p].[UpdatedAtUtc]
                FROM [Payouts] AS [p]
                WHERE [p].[ProviderName] = N'Manual'
                  AND [p].[Status] IN (N'Queued', N'Processing')
                  AND [p].[ProviderTransferId] IS NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [PayoutExecutionReservations] AS [r]
                      WHERE [r].[PayoutId] = [p].[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayoutExecutionReservations");

            migrationBuilder.DropTable(
                name: "PayoutReversals");

            migrationBuilder.DropIndex(
                name: "UX_DriverWithdrawalRequests_PayoutId",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "RequireManualPayoutDualControl",
                table: "SettlementProcessingSettings");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Payouts");

            migrationBuilder.CreateIndex(
                name: "IX_DriverWithdrawalRequests_PayoutId",
                table: "DriverWithdrawalRequests",
                column: "PayoutId");
        }
    }
}
