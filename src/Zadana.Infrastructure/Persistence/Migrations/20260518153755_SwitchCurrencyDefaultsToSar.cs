using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SwitchCurrencyDefaultsToSar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update column defaults from EGP to SAR.
            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Wallet",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "EGP");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "JournalLines",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "EGP");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "JournalEntries",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "EGP");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "FinancialEvents",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "SAR",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "EGP");

            // SAR-only policy: rewrite any pre-existing EGP rows. The system has not
            // operated under a real currency mismatch, so this is a label flip rather
            // than a value conversion. If the deployment ever held real non-SAR money,
            // pause this migration and reconcile manually before continuing.
            migrationBuilder.Sql("UPDATE [Wallet] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';");
            migrationBuilder.Sql("UPDATE [JournalLines] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';");
            migrationBuilder.Sql("UPDATE [JournalEntries] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';");
            migrationBuilder.Sql("UPDATE [FinancialEvents] SET [CurrencyCode] = 'SAR' WHERE [CurrencyCode] = 'EGP';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "Wallet",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EGP",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "SAR");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "JournalLines",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EGP",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "SAR");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "JournalEntries",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EGP",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "SAR");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                table: "FinancialEvents",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "EGP",
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldDefaultValue: "SAR");
        }
    }
}
