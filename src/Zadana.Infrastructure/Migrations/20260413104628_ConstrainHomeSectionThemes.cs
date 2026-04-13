using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConstrainHomeSectionThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [HomeSection]
                SET [Theme] = CASE
                    WHEN LOWER([Theme]) IN ('soft-blue', 'fresh-orange', 'bold-dark') THEN LOWER([Theme])
                    WHEN LOWER([Theme]) = 'theme1' THEN 'soft-blue'
                    WHEN LOWER([Theme]) = 'theme2' THEN 'fresh-orange'
                    WHEN LOWER([Theme]) = 'theme3' THEN 'bold-dark'
                    ELSE 'soft-blue'
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Theme",
                table: "HomeSection",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddCheckConstraint(
                name: "CK_HomeSection_Theme",
                table: "HomeSection",
                sql: "[Theme] IN ('soft-blue', 'fresh-orange', 'bold-dark')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_HomeSection_Theme",
                table: "HomeSection");

            migrationBuilder.AlterColumn<string>(
                name: "Theme",
                table: "HomeSection",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
