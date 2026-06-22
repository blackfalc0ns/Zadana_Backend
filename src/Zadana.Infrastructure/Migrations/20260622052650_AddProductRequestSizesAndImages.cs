using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductRequestSizesAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SuggestedImageUrlsJson",
                table: "ProductRequest",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SuggestedMeasurementValue",
                table: "ProductRequest",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuggestedPackageTypeId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_SuggestedPackageTypeId",
                table: "ProductRequest",
                column: "SuggestedPackageTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRequest_UnitOfMeasure_SuggestedPackageTypeId",
                table: "ProductRequest",
                column: "SuggestedPackageTypeId",
                principalTable: "UnitOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductRequest_UnitOfMeasure_SuggestedPackageTypeId",
                table: "ProductRequest");

            migrationBuilder.DropIndex(
                name: "IX_ProductRequest_SuggestedPackageTypeId",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "SuggestedImageUrlsJson",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "SuggestedMeasurementValue",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "SuggestedPackageTypeId",
                table: "ProductRequest");
        }
    }
}
