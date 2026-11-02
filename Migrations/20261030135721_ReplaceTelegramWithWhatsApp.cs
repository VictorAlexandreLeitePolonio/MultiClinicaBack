using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceTelegramWithWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop/add explícito: o EF propôs renomear TelegramLinkCodeHash -> WhatsAppPhone, o que transformaria
            // hashes de vínculo pendentes em "números de dono". Vínculos do Telegram são descartados; donos reconectam.
            migrationBuilder.DropIndex(name: "IX_Users_TelegramChatId", table: "Users");
            migrationBuilder.DropColumn(name: "TelegramChatId", table: "Users");
            migrationBuilder.DropColumn(name: "TelegramLinkCodeHash", table: "Users");
            migrationBuilder.DropColumn(name: "TelegramLinkExpiresAt", table: "Users");

            migrationBuilder.AddColumn<string>(name: "WhatsAppPhone", table: "Users", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "WhatsAppLinkCodeHash", table: "Users", type: "text", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "WhatsAppLinkExpiresAt", table: "Users",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Users_WhatsAppPhone", table: "Users", column: "WhatsAppPhone", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Users_WhatsAppPhone", table: "Users");
            migrationBuilder.DropColumn(name: "WhatsAppPhone", table: "Users");
            migrationBuilder.DropColumn(name: "WhatsAppLinkCodeHash", table: "Users");
            migrationBuilder.DropColumn(name: "WhatsAppLinkExpiresAt", table: "Users");

            migrationBuilder.AddColumn<long>(name: "TelegramChatId", table: "Users", type: "bigint", nullable: true);
            migrationBuilder.AddColumn<string>(name: "TelegramLinkCodeHash", table: "Users", type: "text", nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "TelegramLinkExpiresAt", table: "Users",
                type: "timestamp with time zone", nullable: true);
            migrationBuilder.CreateIndex(name: "IX_Users_TelegramChatId", table: "Users", column: "TelegramChatId", unique: true);
        }
    }
}
