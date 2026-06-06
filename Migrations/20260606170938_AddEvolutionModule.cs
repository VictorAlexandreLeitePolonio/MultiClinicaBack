using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddEvolutionModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvolutionTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolutionTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvolutionTemplates_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvolutionTemplateFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    MinValue = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxValue = table.Column<decimal>(type: "numeric", nullable: true),
                    TargetValue = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpectedDirection = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    ShowInChart = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolutionTemplateFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvolutionTemplateFields_EvolutionTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "EvolutionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientTreatments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    ProfessionalId = table.Column<int>(type: "integer", nullable: true),
                    TemplateId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "integer", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "PatientEvolutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    TreatmentId = table.Column<int>(type: "integer", nullable: false),
                    ProfessionalId = table.Column<int>(type: "integer", nullable: false),
                    ServiceId = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Conduct = table.Column<string>(type: "text", nullable: true),
                    Observations = table.Column<string>(type: "text", nullable: true),
                    NextGuidance = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientEvolutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientEvolutions_PatientTreatments_TreatmentId",
                        column: x => x.TreatmentId,
                        principalTable: "PatientTreatments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientEvolutions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientEvolutions_Users_ProfessionalId",
                        column: x => x.ProfessionalId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientEvolutionValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false),
                    EvolutionId = table.Column<int>(type: "integer", nullable: false),
                    FieldId = table.Column<int>(type: "integer", nullable: false),
                    ValueNumber = table.Column<decimal>(type: "numeric", nullable: true),
                    ValueText = table.Column<string>(type: "text", nullable: true),
                    ValueBoolean = table.Column<bool>(type: "boolean", nullable: true),
                    ValueJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DeletedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientEvolutionValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientEvolutionValues_EvolutionTemplateFields_FieldId",
                        column: x => x.FieldId,
                        principalTable: "EvolutionTemplateFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientEvolutionValues_PatientEvolutions_EvolutionId",
                        column: x => x.EvolutionId,
                        principalTable: "PatientEvolutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvolutionTemplateFields_ClinicaId",
                table: "EvolutionTemplateFields",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_EvolutionTemplateFields_TemplateId",
                table: "EvolutionTemplateFields",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EvolutionTemplates_ClinicaId",
                table: "EvolutionTemplates",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutions_ClinicaId",
                table: "PatientEvolutions",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutions_PatientId",
                table: "PatientEvolutions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutions_ProfessionalId",
                table: "PatientEvolutions",
                column: "ProfessionalId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutions_TreatmentId",
                table: "PatientEvolutions",
                column: "TreatmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutionValues_ClinicaId",
                table: "PatientEvolutionValues",
                column: "ClinicaId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutionValues_EvolutionId",
                table: "PatientEvolutionValues",
                column: "EvolutionId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvolutionValues_FieldId",
                table: "PatientEvolutionValues",
                column: "FieldId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientEvolutionValues");

            migrationBuilder.DropTable(
                name: "EvolutionTemplateFields");

            migrationBuilder.DropTable(
                name: "PatientEvolutions");

            migrationBuilder.DropTable(
                name: "PatientTreatments");

            migrationBuilder.DropTable(
                name: "EvolutionTemplates");
        }
    }
}
