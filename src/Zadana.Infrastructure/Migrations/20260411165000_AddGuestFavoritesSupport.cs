using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(Zadana.Infrastructure.Persistence.ApplicationDbContext))]
    [Migration("20260411165000_AddGuestFavoritesSupport")]
    public partial class AddGuestFavoritesSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerFavorites_UserId_MasterProductId",
                table: "CustomerFavorites");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "CustomerFavorites",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "GuestId",
                table: "CustomerFavorites",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFavorites_GuestId_MasterProductId",
                table: "CustomerFavorites",
                columns: new[] { "GuestId", "MasterProductId" },
                unique: true,
                filter: "[GuestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFavorites_UserId_MasterProductId",
                table: "CustomerFavorites",
                columns: new[] { "UserId", "MasterProductId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerFavorites_GuestId_MasterProductId",
                table: "CustomerFavorites");

            migrationBuilder.DropIndex(
                name: "IX_CustomerFavorites_UserId_MasterProductId",
                table: "CustomerFavorites");

            migrationBuilder.DropColumn(
                name: "GuestId",
                table: "CustomerFavorites");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "CustomerFavorites",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerFavorites_UserId_MasterProductId",
                table: "CustomerFavorites",
                columns: new[] { "UserId", "MasterProductId" },
                unique: true);
        }
    }
}
