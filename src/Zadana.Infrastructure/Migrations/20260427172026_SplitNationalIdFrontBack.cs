using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitNationalIdFrontBack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NationalIdImageUrl",
                table: "Drivers",
                newName: "NationalIdFrontImageUrl");

            migrationBuilder.AddColumn<string>(
                name: "NationalIdBackImageUrl",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NationalIdBackImageUrl",
                table: "Drivers");

            migrationBuilder.RenameColumn(
                name: "NationalIdFrontImageUrl",
                table: "Drivers",
                newName: "NationalIdImageUrl");
        }
    }
}
