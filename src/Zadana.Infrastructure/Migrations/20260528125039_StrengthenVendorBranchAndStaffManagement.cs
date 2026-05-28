using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StrengthenVendorBranchAndStaffManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "VendorBranch",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "VendorBranch",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "VendorBranch",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ManagerContact",
                table: "VendorBranch",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ManagerName",
                table: "VendorBranch",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "VendorBranch",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                ;WITH BranchSequence AS (
                    SELECT
                        Id,
                        VendorId,
                        ROW_NUMBER() OVER (PARTITION BY VendorId ORDER BY CreatedAtUtc, Id) AS RowNumber
                    FROM VendorBranch
                )
                UPDATE vb
                SET
                    Code = CONCAT('BR-', RIGHT(CONCAT('000', CAST(bs.RowNumber AS varchar(10))), 3)),
                    IsPrimary = CASE WHEN bs.RowNumber = 1 THEN 1 ELSE 0 END,
                    Region = COALESCE(NULLIF(Region, ''), ''),
                    City = COALESCE(NULLIF(City, ''), ''),
                    ManagerName = COALESCE(NULLIF(ManagerName, ''), Name),
                    ManagerContact = COALESCE(NULLIF(ManagerContact, ''), ContactPhone)
                FROM VendorBranch vb
                INNER JOIN BranchSequence bs ON bs.Id = vb.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_VendorBranch_VendorId_Code",
                table: "VendorBranch",
                columns: new[] { "VendorId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorBranch_VendorId_Code",
                table: "VendorBranch");

            migrationBuilder.DropColumn(
                name: "City",
                table: "VendorBranch");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "VendorBranch");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "VendorBranch");

            migrationBuilder.DropColumn(
                name: "ManagerContact",
                table: "VendorBranch");

            migrationBuilder.DropColumn(
                name: "ManagerName",
                table: "VendorBranch");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "VendorBranch");
        }
    }
}
