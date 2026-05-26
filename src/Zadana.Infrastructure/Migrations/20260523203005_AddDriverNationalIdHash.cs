using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverNationalIdHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalIdHash",
                table: "Drivers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_NationalIdHash",
                table: "Drivers",
                column: "NationalIdHash",
                filter: "[NationalIdHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_NationalIdHash",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "NationalIdHash",
                table: "Drivers");
        }
    }
}
