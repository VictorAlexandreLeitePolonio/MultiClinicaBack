using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TotalValue",
                table: "MovimentacoesEstoque",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitValue",
                table: "MovimentacoesEstoque",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "MovimentacoesEstoque");

            migrationBuilder.DropColumn(
                name: "UnitValue",
                table: "MovimentacoesEstoque");
        }
    }
}
