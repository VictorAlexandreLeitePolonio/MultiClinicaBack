using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjetoLP.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Plans",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Plans");
        }
    }
}
