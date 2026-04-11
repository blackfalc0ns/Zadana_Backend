using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandCategoryLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Brand",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                ;WITH RankedBrandCategories AS (
                    SELECT
                        mp.BrandId,
                        mp.CategoryId,
                        ROW_NUMBER() OVER (
                            PARTITION BY mp.BrandId
                            ORDER BY COUNT(*) DESC, MIN(mp.CreatedAtUtc) ASC
                        ) AS Ranking
                    FROM MasterProduct mp
                    WHERE mp.BrandId IS NOT NULL
                    GROUP BY mp.BrandId, mp.CategoryId
                )
                UPDATE b
                SET b.CategoryId = ranked.CategoryId
                FROM Brand b
                INNER JOIN RankedBrandCategories ranked
                    ON ranked.BrandId = b.Id
                   AND ranked.Ranking = 1
                WHERE b.CategoryId IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Brand_CategoryId",
                table: "Brand",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Brand_Category_CategoryId",
                table: "Brand",
                column: "CategoryId",
                principalTable: "Category",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Brand_Category_CategoryId",
                table: "Brand");

            migrationBuilder.DropIndex(
                name: "IX_Brand_CategoryId",
                table: "Brand");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Brand");
        }
    }
}
