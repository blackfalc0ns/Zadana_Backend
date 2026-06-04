using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnsureOrderItemSnapshotColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('dbo.OrderItems', 'SnapshotImageUrl') IS NULL
                    ALTER TABLE [dbo].[OrderItems] ADD [SnapshotImageUrl] nvarchar(2048) NULL;

                IF COL_LENGTH('dbo.OrderItems', 'SnapshotDisplaySize') IS NULL
                    ALTER TABLE [dbo].[OrderItems] ADD [SnapshotDisplaySize] nvarchar(200) NULL;

                IF COL_LENGTH('dbo.OrderItems', 'SnapshotBarcode') IS NULL
                    ALTER TABLE [dbo].[OrderItems] ADD [SnapshotBarcode] nvarchar(100) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: these columns may have been created by older
            // environments before this safety migration existed.
        }
    }
}
