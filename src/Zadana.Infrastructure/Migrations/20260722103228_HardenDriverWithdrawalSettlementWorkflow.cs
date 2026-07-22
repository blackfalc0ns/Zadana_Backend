using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenDriverWithdrawalSettlementWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverWithdrawalRequests_DriverId",
                table: "DriverWithdrawalRequests");

            migrationBuilder.AddColumn<string>(
                name: "DestinationSnapshot",
                table: "DriverWithdrawalRequests",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestIdempotencyKey",
                table: "DriverWithdrawalRequests",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedPayoutDay",
                table: "DriverWithdrawalRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "DriverWithdrawalRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "DriverWithdrawalRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AccountIdentifier",
                table: "DriverPayoutMethods",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "AccountHolderName",
                table: "DriverPayoutMethods",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.Sql("""
                UPDATE withdrawal
                SET [RequestedPayoutDay] = driver.[PayoutDay]
                FROM [DriverWithdrawalRequests] AS withdrawal
                INNER JOIN [Drivers] AS driver ON driver.[Id] = withdrawal.[DriverId]
                WHERE withdrawal.[RequestedPayoutDay] IS NULL;

                DECLARE @DuplicateActiveWithdrawals TABLE ([Id] uniqueidentifier PRIMARY KEY);

                INSERT INTO @DuplicateActiveWithdrawals ([Id])
                SELECT ranked.[Id]
                FROM
                (
                    SELECT
                        withdrawal.[Id],
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY withdrawal.[DriverId]
                            ORDER BY
                                CASE WHEN withdrawal.[Status] = 'Processing' THEN 0 ELSE 1 END,
                                withdrawal.[CreatedAtUtc],
                                withdrawal.[Id]
                        ) AS [RowNumber]
                    FROM [DriverWithdrawalRequests] AS withdrawal
                    WHERE withdrawal.[Status] IN ('Pending', 'Processing')
                ) AS ranked
                WHERE ranked.[RowNumber] > 1;

                UPDATE hold
                SET
                    hold.[Status] = 'Cancelled',
                    hold.[CancelledAtUtc] = SYSUTCDATETIME(),
                    hold.[FailureReason] = COALESCE(
                        hold.[FailureReason],
                        'Duplicate active withdrawal closed during payout safety migration.')
                FROM [WalletHolds] AS hold
                INNER JOIN @DuplicateActiveWithdrawals AS duplicate
                    ON duplicate.[Id] = hold.[ReferenceId]
                WHERE hold.[ReferenceType] = 'DriverWithdrawalRequest'
                  AND hold.[Status] = 'Active';

                UPDATE withdrawal
                SET
                    withdrawal.[Status] = 'Cancelled',
                    withdrawal.[FailureReason] = COALESCE(
                        withdrawal.[FailureReason],
                        'Duplicate active withdrawal closed during payout safety migration.'),
                    withdrawal.[ProcessedAtUtc] = SYSUTCDATETIME()
                FROM [DriverWithdrawalRequests] AS withdrawal
                INNER JOIN @DuplicateActiveWithdrawals AS duplicate
                    ON duplicate.[Id] = withdrawal.[Id];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DriverWithdrawalRequests_Driver_Status",
                table: "DriverWithdrawalRequests",
                columns: new[] { "DriverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_DriverWithdrawalRequests_Driver_IdempotencyKey",
                table: "DriverWithdrawalRequests",
                columns: new[] { "DriverId", "RequestIdempotencyKey" },
                unique: true,
                filter: "[RequestIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DriverWithdrawalRequests_OneActivePerDriver",
                table: "DriverWithdrawalRequests",
                column: "DriverId",
                unique: true,
                filter: "[Status] IN ('Pending', 'Processing')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverWithdrawalRequests_Driver_Status",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "UX_DriverWithdrawalRequests_Driver_IdempotencyKey",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "UX_DriverWithdrawalRequests_OneActivePerDriver",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "DestinationSnapshot",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "RequestIdempotencyKey",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "RequestedPayoutDay",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "DriverWithdrawalRequests");

            migrationBuilder.AlterColumn<string>(
                name: "AccountIdentifier",
                table: "DriverPayoutMethods",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "AccountHolderName",
                table: "DriverPayoutMethods",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.CreateIndex(
                name: "IX_DriverWithdrawalRequests_DriverId",
                table: "DriverWithdrawalRequests",
                column: "DriverId");
        }
    }
}
