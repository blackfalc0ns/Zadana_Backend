using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminAlertOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AdminCatalogPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminDisputesPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminDriversPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminRefundsPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminSettlementsPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminSupportPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminSystemPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AdminVendorsPushEnabled",
                table: "UserPushDevices",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "AdminAlertEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BodyAr = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BodyEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DedupeKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SuppressPush = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAlertEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminAlertDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminAlertEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SignalRSent = table.Column<bool>(type: "bit", nullable: false),
                    PushAttempted = table.Column<bool>(type: "bit", nullable: false),
                    PushSent = table.Column<bool>(type: "bit", nullable: false),
                    PushSkipped = table.Column<bool>(type: "bit", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAlertDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAlertDispatches_AdminAlertEvents_AdminAlertEventId",
                        column: x => x.AdminAlertEventId,
                        principalTable: "AdminAlertEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlertDispatches_AdminAlertEventId_AdminUserId",
                table: "AdminAlertDispatches",
                columns: new[] { "AdminAlertEventId", "AdminUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlertDispatches_AdminUserId_CreatedAtUtc",
                table: "AdminAlertDispatches",
                columns: new[] { "AdminUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlertDispatches_NotificationId",
                table: "AdminAlertDispatches",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlertEvents_DedupeKey_CreatedAtUtc",
                table: "AdminAlertEvents",
                columns: new[] { "DedupeKey", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlertEvents_Status_NextAttemptAtUtc_CreatedAtUtc",
                table: "AdminAlertEvents",
                columns: new[] { "Status", "NextAttemptAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlertEvents_Type_ReferenceId_CreatedAtUtc",
                table: "AdminAlertEvents",
                columns: new[] { "Type", "ReferenceId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAlertDispatches");

            migrationBuilder.DropTable(
                name: "AdminAlertEvents");

            migrationBuilder.DropColumn(
                name: "AdminCatalogPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AdminDisputesPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AdminDriversPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AdminRefundsPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AdminSettlementsPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AdminSupportPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AdminSystemPushEnabled",
                table: "UserPushDevices");

            migrationBuilder.DropColumn(
                name: "AdminVendorsPushEnabled",
                table: "UserPushDevices");
        }
    }
}
