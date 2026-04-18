using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixPaymentCheckoutDeviceIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Payments', 'CheckoutDeviceId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Payments] ADD [CheckoutDeviceId] nvarchar(200) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH('dbo.Payments', 'CheckoutDeviceId') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Payments] DROP COLUMN [CheckoutDeviceId];
                END
                """);
        }
    }
}
