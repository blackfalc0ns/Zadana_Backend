using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverWalletAndDriverMobileApis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DriverPayoutMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MethodType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "BankAccount"),
                    AccountHolderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccountIdentifier = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaskedLabel = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverPayoutMethods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DriverWithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WalletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverPayoutMethodId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    TransferReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverWithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverWithdrawalRequests_DriverPayoutMethods_DriverPayoutMethodId",
                        column: x => x.DriverPayoutMethodId,
                        principalTable: "DriverPayoutMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverWithdrawalRequests_Wallet_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverPayoutMethods_DriverId",
                table: "DriverPayoutMethods",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverWithdrawalRequests_DriverId",
                table: "DriverWithdrawalRequests",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverWithdrawalRequests_DriverPayoutMethodId",
                table: "DriverWithdrawalRequests",
                column: "DriverPayoutMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverWithdrawalRequests_WalletId",
                table: "DriverWithdrawalRequests",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverWithdrawalRequests");

            migrationBuilder.DropTable(
                name: "DriverPayoutMethods");
        }
    }
}
