using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedProductSelectionSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeaturedProductSelectionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectionMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TargetCount = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    MinSalesCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    MinStoreCount = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    RequireDiscount = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ExcludeProductsAlreadyInSpecialOffers = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeaturedProductSelectionSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeaturedProductSelectionSettings");
        }
    }
}
