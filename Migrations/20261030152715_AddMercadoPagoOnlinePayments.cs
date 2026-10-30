using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoOnlinePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MpPaymentId",
                table: "Payments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MpStatus",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MpStatusDetail",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnlineAlert",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidOnline",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentLinkToken",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefundedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PatientAccountId",
                table: "PatientEmailOutbox",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "PaymentId",
                table: "PatientEmailOutbox",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OnlineAcceptCredit",
                table: "Clinicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnlineAcceptDebit",
                table: "Clinicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OnlineAcceptPix",
                table: "Clinicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "OnlineMaxInstallments",
                table: "Clinicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ClinicMercadoPagoAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    MpUserId = table.Column<long>(type: "bigint", nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    RefreshTokenEncrypted = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LiveMode = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicMercadoPagoAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicMercadoPagoAccounts_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MercadoPagoOAuthStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StateHash = table.Column<string>(type: "text", nullable: false),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CodeVerifier = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MercadoPagoOAuthStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentLinkToken",
                table: "Payments",
                column: "PaymentLinkToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMercadoPagoAccounts_ClinicaId",
                table: "ClinicMercadoPagoAccounts",
                column: "ClinicaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMercadoPagoAccounts_MpUserId",
                table: "ClinicMercadoPagoAccounts",
                column: "MpUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MercadoPagoOAuthStates_StateHash",
                table: "MercadoPagoOAuthStates",
                column: "StateHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicMercadoPagoAccounts");

            migrationBuilder.DropTable(
                name: "MercadoPagoOAuthStates");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PaymentLinkToken",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MpPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MpStatus",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MpStatusDetail",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "OnlineAlert",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaidOnline",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentLinkToken",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "PatientEmailOutbox");

            migrationBuilder.DropColumn(
                name: "OnlineAcceptCredit",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "OnlineAcceptDebit",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "OnlineAcceptPix",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "OnlineMaxInstallments",
                table: "Clinicas");

            migrationBuilder.AlterColumn<int>(
                name: "PatientAccountId",
                table: "PatientEmailOutbox",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
