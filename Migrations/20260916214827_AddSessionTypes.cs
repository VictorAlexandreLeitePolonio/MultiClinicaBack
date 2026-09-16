using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MultiClinica.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClinicaId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionTypes_Clinicas_ClinicaId",
                        column: x => x.ClinicaId,
                        principalTable: "Clinicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.RenameColumn(
                name: "TipoSessao",
                table: "Plans",
                newName: "TipoSessaoId");

            migrationBuilder.Sql("""
                INSERT INTO "SessionTypes" ("Name", "ClinicaId")
                SELECT seed."Name", seed."ClinicaId"
                FROM (VALUES
                    ('Fisioterapia', 2), ('Pilates', 2), ('Massagem', 2),
                    ('Hidrolipo', 2), ('Lipedema', 2), ('Linfedema', 2),
                    ('Audiometria Ocupacional', 3), ('Avaliação/Anamnese', 3),
                    ('Consulta', 3), ('Exame Audiometria e Impedanciometria', 3),
                    ('Exame de Audiometria', 3), ('Exame de Audiometria e PAC', 3),
                    ('Exame Impedanciometria', 3), ('Exame Processamento Auditivo Central (PAC)', 3),
                    ('Retorno', 3), ('Retorno de Teste AASI', 3),
                    ('Terapia Fonoaudiológica', 3), ('Terapia Ocupacional', 3),
                    ('Teste de AASI', 3), ('Treinamento auditivo (PAC)', 3)
                ) AS seed("Name", "ClinicaId")
                INNER JOIN "Clinicas" clinic ON clinic."Id" = seed."ClinicaId";

                UPDATE "Plans" plan
                SET "TipoSessaoId" = session_type."Id"
                FROM "SessionTypes" session_type
                WHERE plan."ClinicaId" = 2
                  AND session_type."ClinicaId" = 2
                  AND session_type."Name" = CASE plan."TipoSessaoId"
                      WHEN 0 THEN 'Fisioterapia'
                      WHEN 1 THEN 'Pilates'
                      WHEN 2 THEN 'Massagem'
                      WHEN 3 THEN 'Hidrolipo'
                      WHEN 4 THEN 'Lipedema'
                      WHEN 5 THEN 'Linfedema'
                  END;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Plans" plan
                        LEFT JOIN "SessionTypes" session_type ON session_type."Id" = plan."TipoSessaoId"
                        WHERE session_type."Id" IS NULL OR session_type."ClinicaId" <> plan."ClinicaId"
                    ) THEN
                        RAISE EXCEPTION 'Existem planos sem tipo de sessão válido após a migração.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_TipoSessaoId",
                table: "Plans",
                column: "TipoSessaoId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionTypes_ClinicaId",
                table: "SessionTypes",
                column: "ClinicaId");

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_SessionTypes_ClinicaId_NormalizedName"
                ON "SessionTypes" ("ClinicaId", lower(btrim("Name")));
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_SessionTypes_TipoSessaoId",
                table: "Plans",
                column: "TipoSessaoId",
                principalTable: "SessionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plans_SessionTypes_TipoSessaoId",
                table: "Plans");

            migrationBuilder.Sql("""
                UPDATE "Plans" plan
                SET "TipoSessaoId" = CASE session_type."Name"
                    WHEN 'Fisioterapia' THEN 0
                    WHEN 'Pilates' THEN 1
                    WHEN 'Massagem' THEN 2
                    WHEN 'Hidrolipo' THEN 3
                    WHEN 'Lipedema' THEN 4
                    WHEN 'Linfedema' THEN 5
                END
                FROM "SessionTypes" session_type
                WHERE session_type."Id" = plan."TipoSessaoId"
                  AND session_type."ClinicaId" = 2;
                """);

            migrationBuilder.DropTable(
                name: "SessionTypes");

            migrationBuilder.DropIndex(
                name: "IX_Plans_TipoSessaoId",
                table: "Plans");

            migrationBuilder.RenameColumn(
                name: "TipoSessaoId",
                table: "Plans",
                newName: "TipoSessao");
        }
    }
}
