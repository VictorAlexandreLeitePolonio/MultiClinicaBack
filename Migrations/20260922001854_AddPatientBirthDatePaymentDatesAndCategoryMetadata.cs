using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientBirthDatePaymentDatesAndCategoryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Payments"
                        WHERE "ReferenceMonth" IS NULL
                           OR btrim("ReferenceMonth") !~ '^(0[1-9]|1[0-2])-[0-9]{4}$'
                    ) THEN
                        RAISE EXCEPTION 'Existem referências de pagamento inválidas; esperado MM-YYYY.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Payments"
                        WHERE NOT "IsDeleted"
                        GROUP BY "ClinicaId", "PatientId", to_date("ReferenceMonth", 'MM-YYYY')
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Existem pagamentos duplicados na mesma competência antes da migração.';
                    END IF;
                END $$;

                ALTER TABLE "Payments"
                ALTER COLUMN "ReferenceMonth" TYPE date
                USING to_date("ReferenceMonth", 'MM-YYYY');

                ALTER TABLE "Payments"
                ALTER COLUMN "PaymentDate" TYPE date
                USING ("PaymentDate" AT TIME ZONE 'UTC')::date;

                ALTER TABLE "Payments"
                ALTER COLUMN "PaidAt" TYPE date
                USING ("PaidAt" AT TIME ZONE 'UTC')::date;
                """);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "Patients",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClinicalModelStatus",
                table: "ClinicCategories",
                type: "text",
                nullable: false,
                defaultValue: "NotConfigured");

            migrationBuilder.AddColumn<string>(
                name: "ClinicalProfileKey",
                table: "ClinicCategories",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ClinicCategories",
                type: "text",
                nullable: false,
                defaultValue: "Legacy");

            migrationBuilder.AddColumn<int>(
                name: "ParentCategoryId",
                table: "ClinicCategories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicCategories_ParentCategoryId",
                table: "ClinicCategories",
                column: "ParentCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicCategories_ClinicCategories_ParentCategoryId",
                table: "ClinicCategories",
                column: "ParentCategoryId",
                principalTable: "ClinicCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Payments_ClinicaId_PatientId_ReferenceMonth_Month"
                ON "Payments" (
                    "ClinicaId",
                    "PatientId",
                    make_date(
                        EXTRACT(YEAR FROM "ReferenceMonth")::int,
                        EXTRACT(MONTH FROM "ReferenceMonth")::int,
                        1))
                WHERE "IsDeleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicCategories_ClinicCategories_ParentCategoryId",
                table: "ClinicCategories");

            migrationBuilder.DropIndex(
                name: "IX_ClinicCategories_ParentCategoryId",
                table: "ClinicCategories");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ClinicalModelStatus",
                table: "ClinicCategories");

            migrationBuilder.DropColumn(
                name: "ClinicalProfileKey",
                table: "ClinicCategories");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ClinicCategories");

            migrationBuilder.DropColumn(
                name: "ParentCategoryId",
                table: "ClinicCategories");

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Payments_ClinicaId_PatientId_ReferenceMonth_Month";

                ALTER TABLE "Payments"
                ALTER COLUMN "ReferenceMonth" TYPE text
                USING to_char("ReferenceMonth", 'MM-YYYY');

                ALTER TABLE "Payments"
                ALTER COLUMN "PaymentDate" TYPE timestamp with time zone
                USING ("PaymentDate"::timestamp AT TIME ZONE 'UTC');

                ALTER TABLE "Payments"
                ALTER COLUMN "PaidAt" TYPE timestamp with time zone
                USING ("PaidAt"::timestamp AT TIME ZONE 'UTC');
                """);
        }
    }
}
