// Define o namespace — todos os Models compartilham o mesmo namespace.
namespace MultiClinica.API.Models;

// Enum com os estados possíveis de uma consulta.
public enum AppointmentStatus
{
    Scheduled,  // Consulta agendada (estado inicial)
    Completed,  // Consulta realizada
    Cancelled   // Consulta cancelada
}

// Classe que representa a tabela "Appointments" (Consultas) no banco.
public class Appointment : AuditableEntity
{
    public int ClinicaId { get; set; }
    // Chave estrangeira (FK) — referencia o Id da tabela Users.
    // É o campo que cria o relacionamento 1:N no banco.
    public int UserId { get; set; }
    public int PatientId { get; set; }

    // Data e hora da consulta.
    public DateTime AppointmentDate { get; set; }

    // Estado da consulta — inicia como "Scheduled" por padrão.
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public Clinica Clinica { get; set; } = null!;
    // Navigation property — permite acessar os dados do usuário dono desta consulta.
    // "null!" diz ao compilador: "sei que pode ser nulo, mas garanto que sempre será preenchido pelo EF Core".
    public User User { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}
