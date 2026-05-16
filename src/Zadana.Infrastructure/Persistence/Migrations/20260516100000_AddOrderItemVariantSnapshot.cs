using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderItemVariantSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SnapshotImageUrl",
            table: "OrderItems",
            type: "nvarchar(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SnapshotDisplaySize",
            table: "OrderItems",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SnapshotBarcode",
            table: "OrderItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SnapshotImageUrl",
            table: "OrderItems");

        migrationBuilder.DropColumn(
            name: "SnapshotDisplaySize",
            table: "OrderItems");

        migrationBuilder.DropColumn(
            name: "SnapshotBarcode",
            table: "OrderItems");
    }
}
