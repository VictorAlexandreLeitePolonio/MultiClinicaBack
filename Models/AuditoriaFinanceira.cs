namespace MultiClinica.API.Models;

public class AuditoriaFinanceira
{
    public int Id { get; set; }
    public int ClinicaId { get; set; }
    public int UsuarioId { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public string Acao { get; set; } = string.Empty;
    public string Entidade { get; set; } = string.Empty;
    public int EntidadeId { get; set; }
    public string? DadosAntes { get; set; }
    public string? DadosDepois { get; set; }
    public string? Motivo { get; set; }
    public DateTime DataAcao { get; set; } = DateTime.UtcNow;
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }

    public Clinica Clinica { get; set; } = null!;
}
