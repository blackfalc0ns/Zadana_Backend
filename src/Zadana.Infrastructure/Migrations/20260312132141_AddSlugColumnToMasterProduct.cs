using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugColumnToMasterProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Check if column exists, if not add it, otherwise alter it
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MasterProduct]') AND name = 'Slug')
                BEGIN
                    ALTER TABLE [MasterProduct] ADD [Slug] nvarchar(300) NOT NULL DEFAULT N'';
                END
                ELSE
                BEGIN
                    DECLARE @var sysname;
                    SELECT @var = [d].[name]
                    FROM [sys].[default_constraints] [d]
                    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MasterProduct]') AND [c].[name] = N'Slug');
                    IF @var IS NOT NULL EXEC(N'ALTER TABLE [MasterProduct] DROP CONSTRAINT [' + @var + '];');
                    ALTER TABLE [MasterProduct] ALTER COLUMN [Slug] nvarchar(300) NOT NULL;
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_MasterProduct_Slug",
                table: "MasterProduct",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MasterProduct_Slug",
                table: "MasterProduct");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "MasterProduct",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300);
        }
    }
}
