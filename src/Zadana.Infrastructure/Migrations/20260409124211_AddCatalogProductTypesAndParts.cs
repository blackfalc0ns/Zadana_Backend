using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogProductTypesAndParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PartId",
                table: "MasterProduct",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProductTypeId",
                table: "MasterProduct",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductType_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Part",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProductTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Part", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Part_ProductType_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductType",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterProduct_PartId",
                table: "MasterProduct",
                column: "PartId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterProduct_ProductTypeId",
                table: "MasterProduct",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Part_ProductTypeId_NameEn",
                table: "Part",
                columns: new[] { "ProductTypeId", "NameEn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductType_CategoryId_NameEn",
                table: "ProductType",
                columns: new[] { "CategoryId", "NameEn" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MasterProduct_Part_PartId",
                table: "MasterProduct",
                column: "PartId",
                principalTable: "Part",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MasterProduct_ProductType_ProductTypeId",
                table: "MasterProduct",
                column: "ProductTypeId",
                principalTable: "ProductType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MasterProduct_Part_PartId",
                table: "MasterProduct");

            migrationBuilder.DropForeignKey(
                name: "FK_MasterProduct_ProductType_ProductTypeId",
                table: "MasterProduct");

            migrationBuilder.DropTable(
                name: "Part");

            migrationBuilder.DropTable(
                name: "ProductType");

            migrationBuilder.DropIndex(
                name: "IX_MasterProduct_PartId",
                table: "MasterProduct");

            migrationBuilder.DropIndex(
                name: "IX_MasterProduct_ProductTypeId",
                table: "MasterProduct");

            migrationBuilder.DropColumn(
                name: "PartId",
                table: "MasterProduct");

            migrationBuilder.DropColumn(
                name: "ProductTypeId",
                table: "MasterProduct");
        }
    }
}
