using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnifyProductAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MasterProductImage_ImageBank_ImageBankId",
                table: "MasterProductImage");

            migrationBuilder.DropTable(
                name: "ImageBank");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MasterProductImage",
                table: "MasterProductImage");

            migrationBuilder.DropIndex(
                name: "IX_MasterProductImage_ImageBankId",
                table: "MasterProductImage");

            migrationBuilder.DropColumn(
                name: "ImageBankId",
                table: "MasterProductImage");

            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "MasterProductImage",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AltText",
                table: "MasterProductImage",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_MasterProductImage",
                table: "MasterProductImage",
                columns: new[] { "MasterProductId", "Url" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_MasterProductImage",
                table: "MasterProductImage");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "MasterProductImage");

            migrationBuilder.DropColumn(
                name: "AltText",
                table: "MasterProductImage");

            migrationBuilder.AddColumn<Guid>(
                name: "ImageBankId",
                table: "MasterProductImage",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_MasterProductImage",
                table: "MasterProductImage",
                columns: new[] { "MasterProductId", "ImageBankId" });

            migrationBuilder.CreateTable(
                name: "ImageBank",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByVendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageBank", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterProductImage_ImageBankId",
                table: "MasterProductImage",
                column: "ImageBankId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageBank_Status",
                table: "ImageBank",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ImageBank_UploadedByVendorId",
                table: "ImageBank",
                column: "UploadedByVendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_MasterProductImage_ImageBank_ImageBankId",
                table: "MasterProductImage",
                column: "ImageBankId",
                principalTable: "ImageBank",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
