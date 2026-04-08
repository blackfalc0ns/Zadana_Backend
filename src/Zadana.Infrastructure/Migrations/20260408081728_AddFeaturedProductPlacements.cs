using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedProductPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeaturedProductPlacement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlacementType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VendorProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MasterProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturedProductPlacement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeaturedProductPlacement_MasterProduct_MasterProductId",
                        column: x => x.MasterProductId,
                        principalTable: "MasterProduct",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeaturedProductPlacement_VendorProduct_VendorProductId",
                        column: x => x.VendorProductId,
                        principalTable: "VendorProduct",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeaturedProductPlacement_IsActive_DisplayOrder",
                table: "FeaturedProductPlacement",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FeaturedProductPlacement_MasterProductId",
                table: "FeaturedProductPlacement",
                column: "MasterProductId");

            migrationBuilder.CreateIndex(
                name: "IX_FeaturedProductPlacement_Target",
                table: "FeaturedProductPlacement",
                columns: new[] { "PlacementType", "VendorProductId", "MasterProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeaturedProductPlacement_VendorProductId",
                table: "FeaturedProductPlacement",
                column: "VendorProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeaturedProductPlacement");
        }
    }
}
