using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandVendorWorkspaceAndAdminControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_VendorProduct_VendorProductId",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Carts_Vendor_VendorId",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "IX_Carts_UserId_VendorId",
                table: "Carts");

            migrationBuilder.DropIndex(
                name: "IX_Carts_VendorId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "VendorId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "LineTotal",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "CartItems");

            migrationBuilder.RenameColumn(
                name: "VendorProductId",
                table: "CartItems",
                newName: "MasterProductId");

            migrationBuilder.RenameIndex(
                name: "IX_CartItems_VendorProductId",
                table: "CartItems",
                newName: "IX_CartItems_MasterProductId");

            migrationBuilder.RenameIndex(
                name: "IX_CartItems_CartId_VendorProductId",
                table: "CartItems",
                newName: "IX_CartItems_CartId_MasterProductId");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNote",
                table: "Vendor",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Vendor",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "Vendor",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Vendor",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommercialRegistrationExpiryDate",
                table: "Vendor",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                table: "Vendor",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "Vendor",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdNumber",
                table: "Vendor",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastStatusChangedAtUtc",
                table: "Vendor",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "Vendor",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "Vendor",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAtUtc",
                table: "Vendor",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalAddress",
                table: "Vendor",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nationality",
                table: "Vendor",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "Vendor",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                table: "Vendor",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerPhone",
                table: "Vendor",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutCycle",
                table: "Vendor",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Vendor",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAtUtc",
                table: "Vendor",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "Vendor",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "CartItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLoginLocked",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAtUtc",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId",
                table: "Carts",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_MasterProduct_MasterProductId",
                table: "CartItems",
                column: "MasterProductId",
                principalTable: "MasterProduct",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_MasterProduct_MasterProductId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_Carts_UserId",
                table: "Carts");

            migrationBuilder.DropColumn(
                name: "ApprovalNote",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "CommercialRegistrationExpiryDate",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "IdNumber",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "LastStatusChangedAtUtc",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "LockedAtUtc",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "NationalAddress",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "Nationality",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "OwnerPhone",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "PayoutCycle",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsLoginLocked",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LockReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LockedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "MasterProductId",
                table: "CartItems",
                newName: "VendorProductId");

            migrationBuilder.RenameIndex(
                name: "IX_CartItems_MasterProductId",
                table: "CartItems",
                newName: "IX_CartItems_VendorProductId");

            migrationBuilder.RenameIndex(
                name: "IX_CartItems_CartId_MasterProductId",
                table: "CartItems",
                newName: "IX_CartItems_CartId_VendorProductId");

            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                table: "Carts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "LineTotal",
                table: "CartItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "CartItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_UserId_VendorId",
                table: "Carts",
                columns: new[] { "UserId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Carts_VendorId",
                table: "Carts",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_VendorProduct_VendorProductId",
                table: "CartItems",
                column: "VendorProductId",
                principalTable: "VendorProduct",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Carts_Vendor_VendorId",
                table: "Carts",
                column: "VendorId",
                principalTable: "Vendor",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
