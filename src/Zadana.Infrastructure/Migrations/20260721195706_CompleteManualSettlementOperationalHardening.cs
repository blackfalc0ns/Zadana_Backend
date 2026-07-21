using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteManualSettlementOperationalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SettlementProcessingSettings",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "ScheduledPayoutDay",
                table: "Payouts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProofUrl",
                table: "PayoutReversals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<Guid>(
                name: "ProofAttachmentId",
                table: "PayoutReversals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProofUrl",
                table: "PayoutManualConfirmations",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<Guid>(
                name: "ProofAttachmentId",
                table: "PayoutManualConfirmations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PayoutBankStatementImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    MatchedRows = table.Column<int>(type: "int", nullable: false),
                    UnmatchedRows = table.Column<int>(type: "int", nullable: false),
                    AmbiguousRows = table.Column<int>(type: "int", nullable: false),
                    MismatchRows = table.Column<int>(type: "int", nullable: false),
                    InvalidRows = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutBankStatementImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayoutProofAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProtectedContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutProofAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutProofAttachments_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayoutBankStatementEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    BankReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedBankReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TransactionDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    BeneficiaryMasked = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayoutBankStatementEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayoutBankStatementEntries_PayoutBankStatementImports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "PayoutBankStatementImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayoutBankStatementEntries_Payouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "Payouts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutReversals_ProofAttachmentId",
                table: "PayoutReversals",
                column: "ProofAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutManualConfirmations_ProofAttachmentId",
                table: "PayoutManualConfirmations",
                column: "ProofAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutBankStatementEntries_Reference_Amount",
                table: "PayoutBankStatementEntries",
                columns: new[] { "NormalizedBankReference", "Amount" });

            migrationBuilder.CreateIndex(
                name: "IX_PayoutBankStatementEntries_Status_Date",
                table: "PayoutBankStatementEntries",
                columns: new[] { "Status", "TransactionDateUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PayoutBankStatementEntries_Import_Row",
                table: "PayoutBankStatementEntries",
                columns: new[] { "ImportId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PayoutBankStatementEntries_PayoutId",
                table: "PayoutBankStatementEntries",
                column: "PayoutId",
                unique: true,
                filter: "[PayoutId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayoutBankStatementImports_ImportedAt_ImportedBy",
                table: "PayoutBankStatementImports",
                columns: new[] { "ImportedAtUtc", "ImportedByUserId" });

            migrationBuilder.CreateIndex(
                name: "UX_PayoutBankStatementImports_FileSha256",
                table: "PayoutBankStatementImports",
                column: "FileSha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayoutProofAttachments_PayoutId_Kind_FinalizedAt",
                table: "PayoutProofAttachments",
                columns: new[] { "PayoutId", "Kind", "FinalizedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PayoutProofAttachments_PayoutId_Kind_Sha256",
                table: "PayoutProofAttachments",
                columns: new[] { "PayoutId", "Kind", "Sha256" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PayoutManualConfirmations_PayoutProofAttachments_ProofAttachmentId",
                table: "PayoutManualConfirmations",
                column: "ProofAttachmentId",
                principalTable: "PayoutProofAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayoutReversals_PayoutProofAttachments_ProofAttachmentId",
                table: "PayoutReversals",
                column: "ProofAttachmentId",
                principalTable: "PayoutProofAttachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayoutManualConfirmations_PayoutProofAttachments_ProofAttachmentId",
                table: "PayoutManualConfirmations");

            migrationBuilder.DropForeignKey(
                name: "FK_PayoutReversals_PayoutProofAttachments_ProofAttachmentId",
                table: "PayoutReversals");

            migrationBuilder.DropTable(
                name: "PayoutBankStatementEntries");

            migrationBuilder.DropTable(
                name: "PayoutProofAttachments");

            migrationBuilder.DropTable(
                name: "PayoutBankStatementImports");

            migrationBuilder.DropIndex(
                name: "IX_PayoutReversals_ProofAttachmentId",
                table: "PayoutReversals");

            migrationBuilder.DropIndex(
                name: "IX_PayoutManualConfirmations_ProofAttachmentId",
                table: "PayoutManualConfirmations");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SettlementProcessingSettings");

            migrationBuilder.DropColumn(
                name: "ScheduledPayoutDay",
                table: "Payouts");

            migrationBuilder.DropColumn(
                name: "ProofAttachmentId",
                table: "PayoutReversals");

            migrationBuilder.DropColumn(
                name: "ProofAttachmentId",
                table: "PayoutManualConfirmations");

            migrationBuilder.AlterColumn<string>(
                name: "ProofUrl",
                table: "PayoutReversals",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ProofUrl",
                table: "PayoutManualConfirmations",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
