using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHotPathIndexesForLoad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistories_OrderId",
                table: "OrderStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VendorId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_DriverId",
                table: "DeliveryAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_OrderId",
                table: "DeliveryAssignments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAddresses_UserId",
                table: "CustomerAddresses");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_OrderId_CreatedAt_Desc",
                table: "OrderStatusHistories",
                columns: new[] { "OrderId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentStatus_PlacedAt_Desc",
                table: "Orders",
                columns: new[] { "PaymentStatus", "PlacedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_PlacedAt_Desc",
                table: "Orders",
                columns: new[] { "Status", "PlacedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_PlacedAt_Desc",
                table: "Orders",
                columns: new[] { "UserId", "PlacedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_Status_PlacedAt_Desc",
                table: "Orders",
                columns: new[] { "UserId", "Status", "PlacedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VendorId_BranchId_PlacedAt_Desc",
                table: "Orders",
                columns: new[] { "VendorId", "VendorBranchId", "PlacedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VendorId_PlacedAt_Desc",
                table: "Orders",
                columns: new[] { "VendorId", "PlacedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VendorId_Status_PlacedAt_Desc",
                table: "Orders",
                columns: new[] { "VendorId", "Status", "PlacedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_DriverId_Status_CreatedAt_Desc",
                table: "DeliveryAssignments",
                columns: new[] { "DriverId", "Status", "CreatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_OrderId_CreatedAt_Desc",
                table: "DeliveryAssignments",
                columns: new[] { "OrderId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_OrderId_Status_CreatedAt_Desc",
                table: "DeliveryAssignments",
                columns: new[] { "OrderId", "Status", "CreatedAtUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_UserId_Default_Updated_Created_Desc",
                table: "CustomerAddresses",
                columns: new[] { "UserId", "IsDefault", "UpdatedAtUtc", "CreatedAtUtc" },
                descending: new[] { false, true, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderStatusHistories_OrderId_CreatedAt_Desc",
                table: "OrderStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentStatus_PlacedAt_Desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_PlacedAt_Desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_PlacedAt_Desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_Status_PlacedAt_Desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VendorId_BranchId_PlacedAt_Desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VendorId_PlacedAt_Desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_VendorId_Status_PlacedAt_Desc",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_DriverId_Status_CreatedAt_Desc",
                table: "DeliveryAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_OrderId_CreatedAt_Desc",
                table: "DeliveryAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_OrderId_Status_CreatedAt_Desc",
                table: "DeliveryAssignments");

            migrationBuilder.DropIndex(
                name: "IX_CustomerAddresses_UserId_Default_Updated_Created_Desc",
                table: "CustomerAddresses");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_OrderId",
                table: "OrderStatusHistories",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_VendorId",
                table: "Orders",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_DriverId",
                table: "DeliveryAssignments",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_OrderId",
                table: "DeliveryAssignments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_UserId",
                table: "CustomerAddresses",
                column: "UserId");
        }
    }
}
