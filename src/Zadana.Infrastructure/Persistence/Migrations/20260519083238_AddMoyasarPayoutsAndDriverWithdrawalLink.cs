using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoyasarPayoutsAndDriverWithdrawalLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderSequenceNumber",
                table: "Payouts",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutId",
                table: "DriverWithdrawalRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverWithdrawalRequests_PayoutId",
                table: "DriverWithdrawalRequests",
                column: "PayoutId");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverWithdrawalRequests_Payouts_PayoutId",
                table: "DriverWithdrawalRequests",
                column: "PayoutId",
                principalTable: "Payouts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverWithdrawalRequests_Payouts_PayoutId",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "IX_DriverWithdrawalRequests_PayoutId",
                table: "DriverWithdrawalRequests");

            migrationBuilder.DropColumn(
                name: "ProviderSequenceNumber",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "PayoutId",
                table: "DriverWithdrawalRequests");
        }
    }
}
