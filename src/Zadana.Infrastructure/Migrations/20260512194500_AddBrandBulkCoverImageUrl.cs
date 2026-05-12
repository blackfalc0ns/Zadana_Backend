using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandBulkCoverImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[AdminBrandBulkOperationItems]', N'CoverImageUrl') IS NULL
                BEGIN
                    ALTER TABLE [AdminBrandBulkOperationItems] ADD [CoverImageUrl] nvarchar(1000) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[AdminBrandBulkOperationItems]', N'CoverImageUrl') IS NOT NULL
                BEGIN
                    ALTER TABLE [AdminBrandBulkOperationItems] DROP COLUMN [CoverImageUrl];
                END
                """);
        }
    }
}
