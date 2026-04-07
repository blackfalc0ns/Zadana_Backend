using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncVendorOperationalAndNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptOrders",
                table: "Vendor",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationsEnabled",
                table: "Vendor",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderAmount",
                table: "Vendor",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NewOrdersNotificationsEnabled",
                table: "Vendor",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparationTimeMinutes",
                table: "Vendor",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmsNotificationsEnabled",
                table: "Vendor",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                table: "AspNetUsers",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                table: "AspNetUsers",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptOrders",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "EmailNotificationsEnabled",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "MinimumOrderAmount",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "NewOrdersNotificationsEnabled",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "PreparationTimeMinutes",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "SmsNotificationsEnabled",
                table: "Vendor");

            migrationBuilder.AlterColumn<decimal>(
                name: "Longitude",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Latitude",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(9,6)",
                oldPrecision: 9,
                oldScale: 6,
                oldNullable: true);
        }
    }
}
