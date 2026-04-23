using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverDispatchAndReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "VehicleImageUrl",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PersonalPhotoUrl",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NationalIdImageUrl",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LicenseImageUrl",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Drivers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            AddColumnIfMissing(migrationBuilder, "Drivers", "PrimaryZoneId", "uniqueidentifier NULL");
            AddColumnIfMissing(migrationBuilder, "Drivers", "ReviewNote", "nvarchar(500) NULL");
            AddColumnIfMissing(migrationBuilder, "Drivers", "ReviewedAtUtc", "datetime2 NULL");
            AddColumnIfMissing(migrationBuilder, "Drivers", "ReviewedByUserId", "uniqueidentifier NULL");
            AddColumnIfMissing(migrationBuilder, "Drivers", "SuspensionReason", "nvarchar(500) NULL");
            AddColumnIfMissing(migrationBuilder, "Drivers", "VerificationStatus", "nvarchar(50) NULL");

            CreateTableIfMissing(
                migrationBuilder,
                "DeliveryZones",
                """
                CREATE TABLE [dbo].[DeliveryZones] (
                    [Id] uniqueidentifier NOT NULL,
                    [City] nvarchar(100) NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    [CenterLat] decimal(10,7) NOT NULL,
                    [CenterLng] decimal(10,7) NOT NULL,
                    [RadiusKm] decimal(8,2) NOT NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_DeliveryZones] PRIMARY KEY ([Id])
                );
                """);

            CreateTableIfMissing(
                migrationBuilder,
                "DriverIncidents",
                """
                CREATE TABLE [dbo].[DriverIncidents] (
                    [Id] uniqueidentifier NOT NULL,
                    [DriverId] uniqueidentifier NOT NULL,
                    [IncidentType] nvarchar(200) NOT NULL,
                    [Severity] nvarchar(50) NOT NULL,
                    [Status] nvarchar(50) NOT NULL,
                    [ReviewerName] nvarchar(200) NULL,
                    [LinkedOrderId] uniqueidentifier NULL,
                    [Summary] nvarchar(1000) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_DriverIncidents] PRIMARY KEY ([Id])
                );
                """);

            CreateTableIfMissing(
                migrationBuilder,
                "DriverNotes",
                """
                CREATE TABLE [dbo].[DriverNotes] (
                    [Id] uniqueidentifier NOT NULL,
                    [DriverId] uniqueidentifier NOT NULL,
                    [AuthorUserId] uniqueidentifier NOT NULL,
                    [Message] nvarchar(1000) NOT NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [UpdatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_DriverNotes] PRIMARY KEY ([Id])
                );
                """);

            CreateIndexIfMissing(migrationBuilder, "Drivers", "IX_Drivers_PrimaryZoneId", "[PrimaryZoneId]");
            CreateIndexIfMissing(migrationBuilder, "DriverIncidents", "IX_DriverIncidents_DriverId", "[DriverId]");
            CreateIndexIfMissing(migrationBuilder, "DriverNotes", "IX_DriverNotes_AuthorUserId", "[AuthorUserId]");
            CreateIndexIfMissing(migrationBuilder, "DriverNotes", "IX_DriverNotes_DriverId", "[DriverId]");

            migrationBuilder.Sql(
                """
                UPDATE [Drivers]
                SET [VerificationStatus] = CASE
                    WHEN [Status] IN ('Active', 'Suspended') THEN 'Approved'
                    WHEN NULLIF(LTRIM(RTRIM([NationalIdImageUrl])), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM([LicenseImageUrl])), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM([VehicleImageUrl])), '') IS NOT NULL
                     AND NULLIF(LTRIM(RTRIM([PersonalPhotoUrl])), '') IS NOT NULL
                        THEN 'UnderReview'
                    ELSE 'NeedsDocuments'
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "VerificationStatus",
                table: "Drivers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            AddForeignKeyIfMissing(
                migrationBuilder,
                "DriverIncidents",
                "FK_DriverIncidents_Drivers_DriverId",
                "[DriverId]",
                "Drivers",
                "[Id]",
                "CASCADE");

            AddForeignKeyIfMissing(
                migrationBuilder,
                "DriverNotes",
                "FK_DriverNotes_AspNetUsers_AuthorUserId",
                "[AuthorUserId]",
                "AspNetUsers",
                "[Id]",
                "NO ACTION");

            AddForeignKeyIfMissing(
                migrationBuilder,
                "DriverNotes",
                "FK_DriverNotes_Drivers_DriverId",
                "[DriverId]",
                "Drivers",
                "[Id]",
                "CASCADE");

            AddForeignKeyIfMissing(
                migrationBuilder,
                "Drivers",
                "FK_Drivers_DeliveryZones_PrimaryZoneId",
                "[PrimaryZoneId]",
                "DeliveryZones",
                "[Id]",
                "SET NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropForeignKeyIfExists(migrationBuilder, "Drivers", "FK_Drivers_DeliveryZones_PrimaryZoneId");
            DropForeignKeyIfExists(migrationBuilder, "DriverIncidents", "FK_DriverIncidents_Drivers_DriverId");
            DropForeignKeyIfExists(migrationBuilder, "DriverNotes", "FK_DriverNotes_AspNetUsers_AuthorUserId");
            DropForeignKeyIfExists(migrationBuilder, "DriverNotes", "FK_DriverNotes_Drivers_DriverId");

            DropTableIfExists(migrationBuilder, "DeliveryZones");
            DropTableIfExists(migrationBuilder, "DriverIncidents");
            DropTableIfExists(migrationBuilder, "DriverNotes");

            DropIndexIfExists(migrationBuilder, "Drivers", "IX_Drivers_PrimaryZoneId");

            DropColumnIfExists(migrationBuilder, "Drivers", "PrimaryZoneId");
            DropColumnIfExists(migrationBuilder, "Drivers", "ReviewNote");
            DropColumnIfExists(migrationBuilder, "Drivers", "ReviewedAtUtc");
            DropColumnIfExists(migrationBuilder, "Drivers", "ReviewedByUserId");
            DropColumnIfExists(migrationBuilder, "Drivers", "SuspensionReason");
            DropColumnIfExists(migrationBuilder, "Drivers", "VerificationStatus");

            migrationBuilder.AlterColumn<string>(
                name: "VehicleImageUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PersonalPhotoUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NationalIdImageUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LicenseImageUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column, string definition)
        {
            migrationBuilder.Sql(
                $"""
                IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[{table}] ADD [{column}] {definition};
                END
                """);
        }

        private static void CreateTableIfMissing(MigrationBuilder migrationBuilder, string table, string createTableSql)
        {
            migrationBuilder.Sql(
                $"""
                IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NULL
                BEGIN
                {createTableSql}
                END
                """);
        }

        private static void CreateIndexIfMissing(MigrationBuilder migrationBuilder, string table, string index, string columnsSql)
        {
            migrationBuilder.Sql(
                $"""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'{index}'
                      AND object_id = OBJECT_ID(N'[dbo].[{table}]')
                )
                BEGIN
                    CREATE INDEX [{index}] ON [dbo].[{table}] ({columnsSql});
                END
                """);
        }

        private static void AddForeignKeyIfMissing(
            MigrationBuilder migrationBuilder,
            string table,
            string foreignKey,
            string columnSql,
            string principalTable,
            string principalColumnSql,
            string onDeleteAction)
        {
            migrationBuilder.Sql(
                $"""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'{foreignKey}'
                )
                BEGIN
                    ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{foreignKey}]
                        FOREIGN KEY ({columnSql})
                        REFERENCES [dbo].[{principalTable}] ({principalColumnSql})
                        ON DELETE {onDeleteAction};
                END
                """);
        }

        private static void DropForeignKeyIfExists(MigrationBuilder migrationBuilder, string table, string foreignKey)
        {
            migrationBuilder.Sql(
                $"""
                IF EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'{foreignKey}'
                )
                BEGIN
                    ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{foreignKey}];
                END
                """);
        }

        private static void DropTableIfExists(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.Sql(
                $"""
                IF OBJECT_ID(N'[dbo].[{table}]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[{table}];
                END
                """);
        }

        private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string table, string index)
        {
            migrationBuilder.Sql(
                $"""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE name = N'{index}'
                      AND object_id = OBJECT_ID(N'[dbo].[{table}]')
                )
                BEGIN
                    DROP INDEX [{index}] ON [dbo].[{table}];
                END
                """);
        }

        private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column)
        {
            migrationBuilder.Sql(
                $"""
                IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[{table}] DROP COLUMN [{column}];
                END
                """);
        }
    }
}
