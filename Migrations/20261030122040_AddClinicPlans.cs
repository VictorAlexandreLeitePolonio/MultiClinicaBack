using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CobrancaAtiva",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "DataInicioCobranca",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "DiaVencimento",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "ValorMensalidade",
                table: "Clinicas");

            migrationBuilder.AddColumn<string>(
                name: "AbacateCustomerId",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AbacateSubscriptionId",
                table: "Clinicas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCycle",
                table: "Clinicas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtraProfessionals",
                table: "Clinicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "HubApprovedAt",
                table: "Clinicas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFounder",
                table: "Clinicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHubApproved",
                table: "Clinicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Plan",
                table: "Clinicas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Free");

            migrationBuilder.CreateTable(
                name: "ClinicUsages",
                columns: table => new
                {
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Metric = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Used = table.Column<int>(type: "integer", nullable: false),
                    ExtraGranted = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicUsages", x => new { x.ClinicaId, x.Period, x.Metric });
                });

            migrationBuilder.CreateTable(
                name: "ProcessedWebhookEvents",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "text", nullable: false),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedWebhookEvents", x => new { x.Provider, x.EventId });
                });

            // Clínicas existentes viram fundadoras: Premium de cortesia e já aprovadas no hub.
            migrationBuilder.Sql("""
                UPDATE "Clinicas" SET "Plan" = 'Premium', "IsFounder" = TRUE, "IsHubApproved" = TRUE, "HubApprovedAt" = NOW();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicUsages");

            migrationBuilder.DropTable(
                name: "ProcessedWebhookEvents");

            migrationBuilder.DropColumn(
                name: "AbacateCustomerId",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "AbacateSubscriptionId",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "ExtraProfessionals",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "HubApprovedAt",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "IsFounder",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "IsHubApproved",
                table: "Clinicas");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Clinicas");

            migrationBuilder.AddColumn<bool>(
                name: "CobrancaAtiva",
                table: "Clinicas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataInicioCobranca",
                table: "Clinicas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiaVencimento",
                table: "Clinicas",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMensalidade",
                table: "Clinicas",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
