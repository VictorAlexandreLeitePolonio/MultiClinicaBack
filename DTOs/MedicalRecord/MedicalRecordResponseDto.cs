namespace ProjetoLP.API.DTOs.MedicalRecord;

public class MedicalRecordResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
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
    public DateTime CreatedAt { get; set; }
}
