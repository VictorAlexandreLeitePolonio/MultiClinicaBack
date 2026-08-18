using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientAccountIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PatientAccountId",
                table: "Patients",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PatientAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    CPF = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_PatientAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_PatientAccountId_ClinicaId",
                table: "Patients",
                columns: new[] { "PatientAccountId", "ClinicaId" },
                unique: true,
                filter: "\"PatientAccountId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAccounts_CPF",
                table: "PatientAccounts",
                column: "CPF",
                unique: true,
                filter: "\"CPF\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAccounts_Email",
                table: "PatientAccounts",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_PatientAccounts_PatientAccountId",
                table: "Patients",
                column: "PatientAccountId",
                principalTable: "PatientAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_PatientAccounts_PatientAccountId",
                table: "Patients");

            migrationBuilder.DropTable(
                name: "PatientAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Patients_PatientAccountId_ClinicaId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PatientAccountId",
                table: "Patients");
        }
    }
}
