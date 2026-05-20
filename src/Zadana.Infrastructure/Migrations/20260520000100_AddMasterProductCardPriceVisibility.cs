using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260520000100_AddMasterProductCardPriceVisibility")]
public partial class AddMasterProductCardPriceVisibility : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ShowPriceOnCard",
            table: "MasterProduct",
            type: "bit",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ShowPriceOnCard",
            table: "MasterProduct");
    }
}
