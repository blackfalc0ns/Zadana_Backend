using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

/// <summary>
/// Widens columns that hold PII so that the EF value converter can store
/// the encrypted (longer) ciphertext.
///
/// We do NOT re-encrypt existing rows here — the converter is
/// backward-compatible (rows without the "enc:v1:" prefix are returned as
/// plaintext until the next write, at which point they are encrypted).
/// To force-encrypt all rows, run a one-off `UPDATE ... SET col = col`
/// through the application after deployment.
/// </summary>
public partial class EncryptPiiColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "NationalId",
            table: "Drivers",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LicenseNumber",
            table: "Drivers",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "VehicleLicenseNumber",
            table: "Drivers",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "IBAN",
            table: "VendorBankAccount",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: string.Empty,
            oldClrType: typeof(string),
            oldType: "nvarchar(34)",
            oldMaxLength: 34);

        migrationBuilder.AlterColumn<string>(
            name: "AccountHolderName",
            table: "VendorBankAccount",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: false,
            defaultValue: string.Empty,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "NationalId",
            table: "Drivers",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LicenseNumber",
            table: "Drivers",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "VehicleLicenseNumber",
            table: "Drivers",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "IBAN",
            table: "VendorBankAccount",
            type: "nvarchar(34)",
            maxLength: 34,
            nullable: false,
            defaultValue: string.Empty,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512);

        migrationBuilder.AlterColumn<string>(
            name: "AccountHolderName",
            table: "VendorBankAccount",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: string.Empty,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512);
    }
}
