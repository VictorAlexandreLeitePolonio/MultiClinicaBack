using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjetoLP.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPacientAndPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Observations",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "SessionDate",
                table: "MedicalRecords",
                newName: "Titulo");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "MedicalRecords",
                newName: "SinaisVitais");

            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CPF",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Numero",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rg",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Rua",
                table: "Users",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PacientId",
                table: "Payments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Cirurgias",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Contrato",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DoencaAntiga",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DoencaAtual",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExamesFisicos",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExamesImagem",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Habitos",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Medicamentos",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrientacaoDomiciliar",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OutrasDoencas",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PacientId",
                table: "MedicalRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Patologia",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "QueixaPrincipal",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sessao",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PacientId",
                table: "Appointments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Pacients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    CPF = table.Column<string>(type: "TEXT", nullable: false),
                    Rg = table.Column<string>(type: "TEXT", nullable: false),
                    Rua = table.Column<string>(type: "TEXT", nullable: false),
                    Numero = table.Column<string>(type: "TEXT", nullable: false),
                    Bairro = table.Column<string>(type: "TEXT", nullable: false),
                    Cidade = table.Column<string>(type: "TEXT", nullable: false),
                    Estado = table.Column<string>(type: "TEXT", nullable: false),
                    Cep = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pacients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    TipoPlano = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoSessao = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPlans",
                columns: table => new
                {
                    PaymentsId = table.Column<int>(type: "INTEGER", nullable: false),
                    PlansId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPlans", x => new { x.PaymentsId, x.PlansId });
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Payments_PaymentsId",
                        column: x => x.PaymentsId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentPlans_Plans_PlansId",
                        column: x => x.PlansId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PacientId",
                table: "Payments",
                column: "PacientId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_PacientId",
                table: "MedicalRecords",
                column: "PacientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_PacientId",
                table: "Appointments",
                column: "PacientId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPlans_PlansId",
                table: "PaymentPlans",
                column: "PlansId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Pacients_PacientId",
                table: "Appointments",
                column: "PacientId",
                principalTable: "Pacients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalRecords_Pacients_PacientId",
                table: "MedicalRecords",
                column: "PacientId",
                principalTable: "Pacients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Pacients_PacientId",
                table: "Payments",
                column: "PacientId",
                principalTable: "Pacients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Pacients_PacientId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalRecords_Pacients_PacientId",
                table: "MedicalRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Pacients_PacientId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "Pacients");

            migrationBuilder.DropTable(
                name: "PaymentPlans");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Payments_PacientId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MedicalRecords_PacientId",
                table: "MedicalRecords");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_PacientId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CPF",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Numero",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Rg",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Rua",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PacientId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Cirurgias",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Contrato",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "DoencaAntiga",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "DoencaAtual",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "ExamesFisicos",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "ExamesImagem",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Habitos",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Medicamentos",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "OrientacaoDomiciliar",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "OutrasDoencas",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "PacientId",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Patologia",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "QueixaPrincipal",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Sessao",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "PacientId",
                table: "Appointments");

            migrationBuilder.RenameColumn(
                name: "Titulo",
                table: "MedicalRecords",
                newName: "SessionDate");

            migrationBuilder.RenameColumn(
                name: "SinaisVitais",
                table: "MedicalRecords",
                newName: "Description");

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observations",
                table: "MedicalRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Appointments",
                type: "TEXT",
                nullable: true);
        }
    }
}
