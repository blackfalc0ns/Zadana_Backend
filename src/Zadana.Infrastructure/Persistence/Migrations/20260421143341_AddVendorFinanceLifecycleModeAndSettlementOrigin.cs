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
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Vendor', 'FinancialLifecycleMode') IS NULL
                BEGIN
                    ALTER TABLE [Vendor]
                    ADD [FinancialLifecycleMode] nvarchar(50) NOT NULL DEFAULT N'Weekly';
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Settlements', 'Origin') IS NULL
                BEGIN
                    ALTER TABLE [Settlements]
                    ADD [Origin] nvarchar(50) NOT NULL DEFAULT N'ManualBatch';
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Vendor', 'FinancialLifecycleMode') IS NOT NULL
                BEGIN
                    UPDATE [Vendor]
                    SET [FinancialLifecycleMode] =
                        CASE
                            WHEN [PayoutCycle] = 'biweekly' THEN 'Biweekly'
                            WHEN [PayoutCycle] = 'monthly' THEN 'Monthly'
                            ELSE 'Weekly'
                        END
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Vendor', 'FinancialLifecycleMode') IS NOT NULL
                BEGIN
                    ALTER TABLE [Vendor] DROP COLUMN [FinancialLifecycleMode];
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.Settlements', 'Origin') IS NOT NULL
                BEGIN
                    ALTER TABLE [Settlements] DROP COLUMN [Origin];
                END
                """);
        }
    }
}
