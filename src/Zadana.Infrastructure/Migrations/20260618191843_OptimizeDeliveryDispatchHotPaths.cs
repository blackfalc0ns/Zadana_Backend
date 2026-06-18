using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeDeliveryDispatchHotPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOfferAttempts_OrderId_OfferedAtUtc_Desc",
                table: "DeliveryOfferAttempts",
                columns: new[] { "OrderId", "OfferedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAssignments_Status_OfferExpiresAtUtc",
                table: "DeliveryAssignments",
                columns: new[] { "Status", "OfferExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliveryOfferAttempts_OrderId_OfferedAtUtc_Desc",
                table: "DeliveryOfferAttempts");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryAssignments_Status_OfferExpiresAtUtc",
                table: "DeliveryAssignments");
        }
    }
}
