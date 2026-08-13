using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicTenantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Clinicas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Clinicas");
        }
    }
}
