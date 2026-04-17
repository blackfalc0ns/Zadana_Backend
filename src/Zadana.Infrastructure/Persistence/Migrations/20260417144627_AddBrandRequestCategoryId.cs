using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandRequestCategoryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "BrandRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE br
                SET br.CategoryId = b.CategoryId
                FROM BrandRequest br
                INNER JOIN Brand b ON b.Id = br.CreatedBrandId
                WHERE br.CategoryId IS NULL
                  AND b.CategoryId IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE br
                SET br.CategoryId = pr.SuggestedCategoryId
                FROM BrandRequest br
                INNER JOIN ProductRequest pr ON pr.SuggestedBrandRequestId = br.Id
                WHERE br.CategoryId IS NULL
                  AND pr.SuggestedCategoryId IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE br
                SET br.CategoryId = cr.CreatedCategoryId
                FROM BrandRequest br
                INNER JOIN ProductRequest pr ON pr.SuggestedBrandRequestId = br.Id
                INNER JOIN CategoryRequest cr ON cr.Id = pr.SuggestedCategoryRequestId
                WHERE br.CategoryId IS NULL
                  AND cr.CreatedCategoryId IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE br
                SET br.CategoryId = fallbackCategory.Id
                FROM BrandRequest br
                CROSS APPLY (
                    SELECT TOP (1) c.Id
                    FROM Category c
                    WHERE c.ParentCategoryId IS NOT NULL
                    ORDER BY c.DisplayOrder, c.NameAr, c.Id
                ) AS fallbackCategory
                WHERE br.CategoryId IS NULL;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM BrandRequest WHERE CategoryId IS NULL)
                    THROW 51000, 'Unable to backfill CategoryId for existing BrandRequest rows.', 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "BrandRequest",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandRequest_CategoryId",
                table: "BrandRequest",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_BrandRequest_Category_CategoryId",
                table: "BrandRequest",
                column: "CategoryId",
                principalTable: "Category",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BrandRequest_Category_CategoryId",
                table: "BrandRequest");

            migrationBuilder.DropIndex(
                name: "IX_BrandRequest_CategoryId",
                table: "BrandRequest");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "BrandRequest");
        }
    }
}
