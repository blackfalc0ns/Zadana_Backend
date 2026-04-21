using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorFinanceLifecycleModeAndSettlementOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FinancialLifecycleMode",
                table: "Vendor",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Weekly");

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "Settlements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "ManualBatch");

            migrationBuilder.Sql("""
                UPDATE [Vendor]
                SET [FinancialLifecycleMode] =
                    CASE
                        WHEN [PayoutCycle] = 'biweekly' THEN 'Biweekly'
                        WHEN [PayoutCycle] = 'monthly' THEN 'Monthly'
                        ELSE 'Weekly'
                    END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinancialLifecycleMode",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "Settlements");
        }
    }
}
