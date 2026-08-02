using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260802130000_MakeCustomerAddressContactPhoneOptional")]
public partial class MakeCustomerAddressContactPhoneOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ContactPhone",
            table: "CustomerAddresses",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE CustomerAddresses SET ContactPhone = N'' WHERE ContactPhone IS NULL");

        migrationBuilder.AlterColumn<string>(
            name: "ContactPhone",
            table: "CustomerAddresses",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(50)",
            oldMaxLength: 50,
            oldNullable: true);
    }
}
