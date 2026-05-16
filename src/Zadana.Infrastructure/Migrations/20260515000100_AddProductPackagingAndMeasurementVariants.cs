using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260515000100_AddProductPackagingAndMeasurementVariants")]
public partial class AddProductPackagingAndMeasurementVariants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            table: "UnitOfMeasure",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Measurement");

        migrationBuilder.AddColumn<Guid>(
            name: "VariantGroupId",
            table: "MasterProduct",
            type: "uniqueidentifier",
            nullable: false,
            defaultValue: Guid.Empty);

        migrationBuilder.AddColumn<Guid>(
            name: "PackageTypeId",
            table: "MasterProduct",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MeasurementValue",
            table: "MasterProduct",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "MeasurementUnitId",
            table: "MasterProduct",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE UnitOfMeasure
            SET Kind = CASE
                WHEN NameEn IN ('Piece','Pack','Box','Carton','Case','Bottle','Jar','Can','Pouch','Sachet','Bag','Roll','Sheet','Pair','Set','Bundle','Dozen','Tray','Crate','Pallet','Strip','Blister','Tube','Bar','Loaf','Slice','Capsule','Tablet','Vial','Ampoule')
                    THEN 'Packaging'
                ELSE 'Measurement'
            END;
            """);

        migrationBuilder.Sql("""
            UPDATE MasterProduct
            SET VariantGroupId = Id
            WHERE VariantGroupId = '00000000-0000-0000-0000-000000000000';
            """);

        migrationBuilder.Sql("""
            UPDATE mp
            SET MeasurementUnitId = mp.UnitOfMeasureId
            FROM MasterProduct mp
            INNER JOIN UnitOfMeasure u ON u.Id = mp.UnitOfMeasureId
            WHERE u.Kind = 'Measurement' AND mp.UnitOfMeasureId IS NOT NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE mp
            SET PackageTypeId = mp.UnitOfMeasureId
            FROM MasterProduct mp
            INNER JOIN UnitOfMeasure u ON u.Id = mp.UnitOfMeasureId
            WHERE u.Kind = 'Packaging' AND mp.UnitOfMeasureId IS NOT NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_MasterProduct_MeasurementUnitId",
            table: "MasterProduct",
            column: "MeasurementUnitId");

        migrationBuilder.CreateIndex(
            name: "IX_MasterProduct_PackageTypeId",
            table: "MasterProduct",
            column: "PackageTypeId");

        migrationBuilder.CreateIndex(
            name: "IX_MasterProduct_VariantGroupId",
            table: "MasterProduct",
            column: "VariantGroupId");

        migrationBuilder.AddForeignKey(
            name: "FK_MasterProduct_UnitOfMeasure_MeasurementUnitId",
            table: "MasterProduct",
            column: "MeasurementUnitId",
            principalTable: "UnitOfMeasure",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_MasterProduct_UnitOfMeasure_PackageTypeId",
            table: "MasterProduct",
            column: "PackageTypeId",
            principalTable: "UnitOfMeasure",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_MasterProduct_UnitOfMeasure_MeasurementUnitId",
            table: "MasterProduct");

        migrationBuilder.DropForeignKey(
            name: "FK_MasterProduct_UnitOfMeasure_PackageTypeId",
            table: "MasterProduct");

        migrationBuilder.DropIndex(
            name: "IX_MasterProduct_MeasurementUnitId",
            table: "MasterProduct");

        migrationBuilder.DropIndex(
            name: "IX_MasterProduct_PackageTypeId",
            table: "MasterProduct");

        migrationBuilder.DropIndex(
            name: "IX_MasterProduct_VariantGroupId",
            table: "MasterProduct");

        migrationBuilder.DropColumn(
            name: "Kind",
            table: "UnitOfMeasure");

        migrationBuilder.DropColumn(
            name: "VariantGroupId",
            table: "MasterProduct");

        migrationBuilder.DropColumn(
            name: "PackageTypeId",
            table: "MasterProduct");

        migrationBuilder.DropColumn(
            name: "MeasurementValue",
            table: "MasterProduct");

        migrationBuilder.DropColumn(
            name: "MeasurementUnitId",
            table: "MasterProduct");
    }
}
