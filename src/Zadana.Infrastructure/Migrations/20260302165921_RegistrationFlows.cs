using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zadana.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommercialRegisterDocumentUrl",
                table: "Vendor",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Vendor",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "User",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "User",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoUrl",
                table: "User",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseImageUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NationalIdImageUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalPhotoUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleImageUrl",
                table: "Drivers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommercialRegisterDocumentUrl",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Vendor");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "User");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoUrl",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "LicenseImageUrl",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "NationalIdImageUrl",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "PersonalPhotoUrl",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "VehicleImageUrl",
                table: "Drivers");
        }
    }
}
