using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class ProntuarioSemAcompanhamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientEvolutions_PatientTreatments_TreatmentId",
                table: "PatientEvolutions");

            migrationBuilder.DropTable(
                name: "PatientTreatments");

            migrationBuilder.DropIndex(
                name: "IX_PatientEvolutions_TreatmentId",
                table: "PatientEvolutions");

            migrationBuilder.DropColumn(
                name: "TreatmentId",
                table: "PatientEvolutions");

            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "PatientEvolutions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutions_AppointmentId",
                table: "PatientEvolutions",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutions_ClinicaId_PatientId_Date",
                table: "PatientEvolutions",
                columns: new[] { "ClinicaId", "PatientId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_PatientEvolutions_Appointments_AppointmentId",
                table: "PatientEvolutions",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientEvolutions_Appointments_AppointmentId",
                table: "PatientEvolutions");

            migrationBuilder.DropIndex(
                name: "IX_PatientEvolutions_AppointmentId",
                table: "PatientEvolutions");

            migrationBuilder.DropIndex(
                name: "IX_PatientEvolutions_ClinicaId_PatientId_Date",
                table: "PatientEvolutions");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "PatientEvolutions");

            migrationBuilder.AddColumn<int>(
                name: "TreatmentId",
                table: "PatientEvolutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PatientTreatments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    ProfessionalId = table.Column<int>(type: "integer", nullable: true),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientTreatments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientTreatments_EvolutionTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "EvolutionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientTreatments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientTreatments_Users_ProfessionalId",
                        column: x => x.ProfessionalId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutions_TreatmentId",
                table: "PatientEvolutions",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTreatments_ClinicaId",
                table: "PatientTreatments",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTreatments_PatientId",
                table: "PatientTreatments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTreatments_ProfessionalId",
                table: "PatientTreatments",
                column: "ProfessionalId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientTreatments_TemplateId",
                table: "PatientTreatments",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientEvolutions_PatientTreatments_TreatmentId",
                table: "PatientEvolutions",
                column: "TreatmentId",
                principalTable: "PatientTreatments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
