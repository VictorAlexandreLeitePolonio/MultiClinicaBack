// Define o namespace — organiza o arquivo dentro da pasta Models.
namespace MultiClinica.API.Models;

// Classe que representa a tabela "MedicalRecords" (Prontuários) no banco.
public class MedicalRecord : AuditableEntity
{
    public int ClinicaId { get; set; }
    // Chave estrangeira — liga o prontuário ao usuário/paciente dono dele.
    public int UserId { get; set; }
    public int PatientId { get; set; }
    public string Patologia { get; set; } = string.Empty;
    public string QueixaPrincipal { get; set; } = string.Empty;
    public string ExamesImagem { get; set; } = string.Empty;
    public string DoencaAntiga { get; set; } = string.Empty;
    public string DoencaAtual { get; set; } = string.Empty;
    public string Habitos { get; set; } = string.Empty;
    public string ExamesFisicos { get; set; } = string.Empty;
    public string SinaisVitais { get; set; } = string.Empty;
    public string Medicamentos { get; set; } = string.Empty;
    public string Cirurgias { get; set; } = string.Empty;
    public string OutrasDoencas { get; set; } = string.Empty;
    public string Sessao { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Contrato { get; set; } = string.Empty;
    public string OrientacaoDomiciliar { get; set; } = string.Empty;
    public Clinica Clinica { get; set; } = null!;
    public User User { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public ICollection<ClinicalAttachment> Attachments { get; set; } = [];
}
