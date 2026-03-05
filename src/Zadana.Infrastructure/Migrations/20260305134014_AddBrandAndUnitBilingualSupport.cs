using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandAndUnitBilingualSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Brand",
                newName: "NameEn");

            migrationBuilder.AddColumn<string>(
                name: "Symbol",
                table: "UnitOfMeasure",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Brand",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Symbol",
                table: "UnitOfMeasure");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "Brand");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "Brand",
                newName: "Name");
        }
    }
}
