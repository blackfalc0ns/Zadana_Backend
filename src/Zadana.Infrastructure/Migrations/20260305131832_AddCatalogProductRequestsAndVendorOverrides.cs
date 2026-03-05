using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogProductRequestsAndVendorOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomDescriptionAr",
                table: "VendorProduct",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomDescriptionEn",
                table: "VendorProduct",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomNameAr",
                table: "VendorProduct",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomNameEn",
                table: "VendorProduct",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "ImageBank",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ImageBank",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UploadedByVendorId",
                table: "ImageBank",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductRequest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedNameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SuggestedNameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SuggestedDescriptionAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SuggestedDescriptionEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SuggestedCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRequest", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRequest_Category_SuggestedCategoryId",
                        column: x => x.SuggestedCategoryId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductRequest_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageBank_Status",
                table: "ImageBank",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImageBank_UploadedByVendorId",
                table: "ImageBank",
                column: "UploadedByVendorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_Status",
                table: "ProductRequest",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_SuggestedCategoryId",
                table: "ProductRequest",
                column: "SuggestedCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRequest_VendorId",
                table: "ProductRequest",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductRequest");

            migrationBuilder.DropIndex(
                name: "IX_ImageBank_Status",
                table: "ImageBank");

            migrationBuilder.DropIndex(
                name: "IX_ImageBank_UploadedByVendorId",
                table: "ImageBank");

            migrationBuilder.DropColumn(
                name: "CustomDescriptionAr",
                table: "VendorProduct");

            migrationBuilder.DropColumn(
                name: "CustomDescriptionEn",
                table: "VendorProduct");

            migrationBuilder.DropColumn(
                name: "CustomNameAr",
                table: "VendorProduct");

            migrationBuilder.DropColumn(
                name: "CustomNameEn",
                table: "VendorProduct");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "ImageBank");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ImageBank");

            migrationBuilder.DropColumn(
                name: "UploadedByVendorId",
                table: "ImageBank");
        }
    }
}
