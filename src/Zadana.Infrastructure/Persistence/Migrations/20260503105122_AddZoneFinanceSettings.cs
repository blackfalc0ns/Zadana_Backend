using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZoneFinanceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZoneFinanceSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryZoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VatPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CodFeeType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CodFlatFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CodPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsVatActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCodFeeActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneFinanceSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZoneFinanceSettings_DeliveryZoneId",
                table: "ZoneFinanceSettings",
                column: "DeliveryZoneId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZoneFinanceSettings");
        }
    }
}
