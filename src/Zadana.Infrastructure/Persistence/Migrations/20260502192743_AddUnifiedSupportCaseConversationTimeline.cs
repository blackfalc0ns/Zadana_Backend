using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnifiedSupportCaseConversationTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwaitingResponseFromRole",
                table: "OrderSupportCases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "OrderSupportCaseActivities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "all_external");

            migrationBuilder.AddColumn<bool>(
                name: "IsInternalOnly",
                table: "OrderSupportCaseActivities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "OrderSupportCaseActivities",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "system");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AwaitingResponseFromRole",
                table: "OrderSupportCases");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "OrderSupportCaseActivities");

            migrationBuilder.DropColumn(
                name: "IsInternalOnly",
                table: "OrderSupportCaseActivities");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "OrderSupportCaseActivities");
        }
    }
}
