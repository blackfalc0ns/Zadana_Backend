using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncSystemLogsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceApp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetEntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QueryString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequestPayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogEntries_ActorUserId",
                table: "SystemLogEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogEntries_IsSuccess",
                table: "SystemLogEntries",
                column: "IsSuccess");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogEntries_OccurredAtUtc",
                table: "SystemLogEntries",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogEntries_SourceApp_Module_OccurredAtUtc",
                table: "SystemLogEntries",
                columns: new[] { "SourceApp", "Module", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemLogEntries_TargetEntityType_TargetEntityId",
                table: "SystemLogEntries",
                columns: new[] { "TargetEntityType", "TargetEntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemLogEntries");
        }
    }
}
