using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailDispatchLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RuleLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AudienceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ToRecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcRecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BccRecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EventKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsTestSend = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDispatchLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailSenderProfileConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReplyTo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DescriptionKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSenderProfileConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailWorkflowRuleConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubtitleKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CategoryKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CadenceLabelKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TriggerNotesKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    SenderProfileKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AudienceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PanelScope = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PersonaTargetsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityScopeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BranchScopeMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecipientTargetsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RouteJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AutomationState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailWorkflowRuleConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDispatchLogs_RuleKey_CreatedAtUtc",
                table: "EmailDispatchLogs",
                columns: new[] { "RuleKey", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDispatchLogs_Source_Status_CreatedAtUtc",
                table: "EmailDispatchLogs",
                columns: new[] { "Source", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSenderProfileConfigs_ProfileKey",
                table: "EmailSenderProfileConfigs",
                column: "ProfileKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailWorkflowRuleConfigs_EventKey",
                table: "EmailWorkflowRuleConfigs",
                column: "EventKey");

            migrationBuilder.CreateIndex(
                name: "IX_EmailWorkflowRuleConfigs_RuleKey",
                table: "EmailWorkflowRuleConfigs",
                column: "RuleKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailDispatchLogs");

            migrationBuilder.DropTable(
                name: "EmailSenderProfileConfigs");

            migrationBuilder.DropTable(
                name: "EmailWorkflowRuleConfigs");
        }
    }
}
