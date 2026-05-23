using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorSupportTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorSupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LastMessagePreview = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FirstResponseAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorSupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorSupportTickets_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_VendorSupportTickets_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VendorSupportTicketMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorSupportTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorSupportTicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorSupportTicketMessages_VendorSupportTickets_VendorSupportTicketId",
                        column: x => x.VendorSupportTicketId,
                        principalTable: "VendorSupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorSupportTicketMessages_AuthorUserId",
                table: "VendorSupportTicketMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorSupportTicketMessages_VendorSupportTicketId_CreatedAtUtc",
                table: "VendorSupportTicketMessages",
                columns: new[] { "VendorSupportTicketId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorSupportTickets_OrderId",
                table: "VendorSupportTickets",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorSupportTickets_Reference",
                table: "VendorSupportTickets",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorSupportTickets_VendorId_Status_UpdatedAtUtc",
                table: "VendorSupportTickets",
                columns: new[] { "VendorId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VendorSupportTickets_VendorId_UpdatedAtUtc",
                table: "VendorSupportTickets",
                columns: new[] { "VendorId", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorSupportTicketMessages");

            migrationBuilder.DropTable(
                name: "VendorSupportTickets");
        }
    }
}
