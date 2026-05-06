using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverRealtimeNotificationProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AccountPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AssignmentPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DispatchPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "WalletPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Notifications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Notifications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Category_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "UserId", "Category", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_Priority_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "UserId", "Priority", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Category_CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_Priority_CreatedAtUtc",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AccountPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AssignmentPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "DispatchPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "SupportPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "WalletPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Notifications");
        }
    }
}
