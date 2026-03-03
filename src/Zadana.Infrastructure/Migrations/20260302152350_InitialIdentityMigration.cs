using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialIdentityMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshToken_Token",
                table: "RefreshToken",
                column: "Token");
        }
    }
}
