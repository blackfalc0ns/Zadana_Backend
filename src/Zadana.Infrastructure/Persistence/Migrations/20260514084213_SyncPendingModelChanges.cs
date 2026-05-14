using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NotificationSound",
                table: "Vendor",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "classic");

            migrationBuilder.AddColumn<string>(
                name: "NotificationSound",
                table: "UserPushDevices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "classic");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotificationSound",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "NotificationSound",
                table: "UserPushDevices");
        }
    }
}
