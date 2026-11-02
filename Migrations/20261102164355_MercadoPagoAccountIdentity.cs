using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class MercadoPagoAccountIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MpEmail",
                table: "ClinicMercadoPagoAccounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MpNickname",
                table: "ClinicMercadoPagoAccounts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MpEmail",
                table: "ClinicMercadoPagoAccounts");

            migrationBuilder.DropColumn(
                name: "MpNickname",
                table: "ClinicMercadoPagoAccounts");
        }
    }
}
