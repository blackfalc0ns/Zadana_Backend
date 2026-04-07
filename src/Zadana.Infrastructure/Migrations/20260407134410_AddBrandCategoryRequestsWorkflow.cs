using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandCategoryRequestsWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SuggestedCategoryId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedMasterProductId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "ProductRequest",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "ProductRequest",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuggestedBrandId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuggestedBrandRequestId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuggestedCategoryRequestId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuggestedUnitOfMeasureId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BrandRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedBrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandRequest_Brand_CreatedBrandId",
                        column: x => x.CreatedBrandId,
                        principalTable: "Brand",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BrandRequest_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategoryRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryRequest_Category_CreatedCategoryId",
                        column: x => x.CreatedCategoryId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoryRequest_Category_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategoryRequest_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_CreatedMasterProductId",
                table: "ProductRequest",
                column: "CreatedMasterProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_SuggestedBrandId",
                table: "ProductRequest",
                column: "SuggestedBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_SuggestedBrandRequestId",
                table: "ProductRequest",
                column: "SuggestedBrandRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_SuggestedCategoryRequestId",
                table: "ProductRequest",
                column: "SuggestedCategoryRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_SuggestedUnitOfMeasureId",
                table: "ProductRequest",
                column: "SuggestedUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandRequest_CreatedBrandId",
                table: "BrandRequest",
                column: "CreatedBrandId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandRequest_Status",
                table: "BrandRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BrandRequest_VendorId",
                table: "BrandRequest",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRequest_CreatedCategoryId",
                table: "CategoryRequest",
                column: "CreatedCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRequest_ParentCategoryId",
                table: "CategoryRequest",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRequest_Status",
                table: "CategoryRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRequest_VendorId",
                table: "CategoryRequest",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRequest_BrandRequest_SuggestedBrandRequestId",
                table: "ProductRequest",
                column: "SuggestedBrandRequestId",
                principalTable: "BrandRequest",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRequest_Brand_SuggestedBrandId",
                table: "ProductRequest",
                column: "SuggestedBrandId",
                principalTable: "Brand",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRequest_CategoryRequest_SuggestedCategoryRequestId",
                table: "ProductRequest",
                column: "SuggestedCategoryRequestId",
                principalTable: "CategoryRequest",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRequest_MasterProduct_CreatedMasterProductId",
                table: "ProductRequest",
                column: "CreatedMasterProductId",
                principalTable: "MasterProduct",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductRequest_UnitOfMeasure_SuggestedUnitOfMeasureId",
                table: "ProductRequest",
                column: "SuggestedUnitOfMeasureId",
                principalTable: "UnitOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductRequest_BrandRequest_SuggestedBrandRequestId",
                table: "ProductRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductRequest_Brand_SuggestedBrandId",
                table: "ProductRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductRequest_CategoryRequest_SuggestedCategoryRequestId",
                table: "ProductRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductRequest_MasterProduct_CreatedMasterProductId",
                table: "ProductRequest");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductRequest_UnitOfMeasure_SuggestedUnitOfMeasureId",
                table: "ProductRequest");

            migrationBuilder.DropTable(
                name: "BrandRequest");

            migrationBuilder.DropTable(
                name: "CategoryRequest");

            migrationBuilder.DropIndex(
                name: "IX_ProductRequest_CreatedMasterProductId",
                table: "ProductRequest");

            migrationBuilder.DropIndex(
                name: "IX_ProductRequest_SuggestedBrandId",
                table: "ProductRequest");

            migrationBuilder.DropIndex(
                name: "IX_ProductRequest_SuggestedBrandRequestId",
                table: "ProductRequest");

            migrationBuilder.DropIndex(
                name: "IX_ProductRequest_SuggestedCategoryRequestId",
                table: "ProductRequest");

            migrationBuilder.DropIndex(
                name: "IX_ProductRequest_SuggestedUnitOfMeasureId",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "CreatedMasterProductId",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "SuggestedBrandId",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "SuggestedBrandRequestId",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "SuggestedCategoryRequestId",
                table: "ProductRequest");

            migrationBuilder.DropColumn(
                name: "SuggestedUnitOfMeasureId",
                table: "ProductRequest");

            migrationBuilder.AlterColumn<Guid>(
                name: "SuggestedCategoryId",
                table: "ProductRequest",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
