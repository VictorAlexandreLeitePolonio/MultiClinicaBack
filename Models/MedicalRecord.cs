// Define o namespace — organiza o arquivo dentro da pasta Models.
namespace ProjetoLP.API.Models;

// Classe que representa a tabela "MedicalRecords" (Prontuários) no banco.
public class MedicalRecord
{
    // Chave primária com autoincrement.
    public int Id { get; set; }

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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property — permite acessar os dados do paciente via: medicalRecord.User.Name
    public User User { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}
