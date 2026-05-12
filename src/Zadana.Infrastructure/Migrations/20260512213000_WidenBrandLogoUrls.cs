using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    [Migration("20260512213000_WidenBrandLogoUrls")]
    public partial class WidenBrandLogoUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[Brand]', N'LogoUrl') IS NOT NULL
                BEGIN
                    ALTER TABLE [Brand] ALTER COLUMN [LogoUrl] nvarchar(1000) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[AdminBrandBulkOperationItems]', N'LogoUrl') IS NOT NULL
                BEGIN
                    ALTER TABLE [AdminBrandBulkOperationItems] ALTER COLUMN [LogoUrl] nvarchar(1000) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[Brand]', N'LogoUrl') IS NOT NULL
                BEGIN
                    ALTER TABLE [Brand] ALTER COLUMN [LogoUrl] nvarchar(500) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[AdminBrandBulkOperationItems]', N'LogoUrl') IS NOT NULL
                BEGIN
                    ALTER TABLE [AdminBrandBulkOperationItems] ALTER COLUMN [LogoUrl] nvarchar(500) NULL;
                END
                """);
        }
    }
}
