using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Zadana.Infrastructure.Persistence;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260513000100_EnsureBrandCategoriesTable")]
    public partial class EnsureBrandCategoriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[BrandCategories]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [BrandCategories] (
                        [Id] uniqueidentifier NOT NULL,
                        [BrandId] uniqueidentifier NOT NULL,
                        [CategoryId] uniqueidentifier NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [UpdatedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_BrandCategories] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_BrandCategories_Brand_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brand] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_BrandCategories_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([Id]) ON DELETE NO ACTION
                    );
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[BrandCategories]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_BrandCategories_BrandId_CategoryId'
                       AND object_id = OBJECT_ID(N'[BrandCategories]')
                   )
                BEGIN
                    CREATE UNIQUE INDEX [IX_BrandCategories_BrandId_CategoryId] ON [BrandCategories] ([BrandId], [CategoryId]);
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[BrandCategories]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE name = N'IX_BrandCategories_CategoryId'
                       AND object_id = OBJECT_ID(N'[BrandCategories]')
                   )
                BEGIN
                    CREATE INDEX [IX_BrandCategories_CategoryId] ON [BrandCategories] ([CategoryId]);
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[BrandCategories]', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [BrandCategories] ([Id], [BrandId], [CategoryId], [CreatedAtUtc], [UpdatedAtUtc])
                    SELECT NEWID(), [brand].[Id], [brand].[CategoryId], SYSUTCDATETIME(), SYSUTCDATETIME()
                    FROM [Brand] AS [brand]
                    WHERE [brand].[CategoryId] IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [BrandCategories] AS [link]
                          WHERE [link].[BrandId] = [brand].[Id]
                            AND [link].[CategoryId] = [brand].[CategoryId]
                      );
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
