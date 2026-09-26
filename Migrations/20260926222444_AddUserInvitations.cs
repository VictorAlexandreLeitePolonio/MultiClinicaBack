using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvitationTokenHash",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitationExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "InvitationTokenHash",
                table: "Users");
        }
    }
}
