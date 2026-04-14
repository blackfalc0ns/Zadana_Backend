using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorProductBulkOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorProductBulkOperation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ProcessedRows = table.Column<int>(type: "int", nullable: false),
                    SucceededRows = table.Column<int>(type: "int", nullable: false),
                    FailedRows = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorProductBulkOperation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorProductBulkOperation_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VendorProductBulkOperationItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    MasterProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorBranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CompareAtPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    StockQty = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MinOrderQty = table.Column<int>(type: "int", nullable: false),
                    MaxOrderQty = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedVendorProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorProductBulkOperationItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorProductBulkOperationItem_MasterProduct_MasterProductId",
                        column: x => x.MasterProductId,
                        principalTable: "MasterProduct",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorProductBulkOperationItem_VendorBranch_VendorBranchId",
                        column: x => x.VendorBranchId,
                        principalTable: "VendorBranch",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VendorProductBulkOperationItem_VendorProductBulkOperation_OperationId",
                        column: x => x.OperationId,
                        principalTable: "VendorProductBulkOperation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductBulkOperation_IdempotencyKey",
                table: "VendorProductBulkOperation",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductBulkOperation_Status",
                table: "VendorProductBulkOperation",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductBulkOperation_VendorId",
                table: "VendorProductBulkOperation",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductBulkOperationItem_MasterProductId",
                table: "VendorProductBulkOperationItem",
                column: "MasterProductId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductBulkOperationItem_OperationId",
                table: "VendorProductBulkOperationItem",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductBulkOperationItem_OperationId_RowNumber",
                table: "VendorProductBulkOperationItem",
                columns: new[] { "OperationId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorProductBulkOperationItem_VendorBranchId",
                table: "VendorProductBulkOperationItem",
                column: "VendorBranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorProductBulkOperationItem");

            migrationBuilder.DropTable(
                name: "VendorProductBulkOperation");
        }
    }
}
