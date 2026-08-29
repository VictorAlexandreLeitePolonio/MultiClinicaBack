namespace MultiClinica.API.DTOs.Financial;

// ponytail: Ip/UserAgent ficam fora da resposta de propósito (dados de rastreio).
public class AuditoriaFinanceiraDto
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Acao { get; set; } = string.Empty;
    public string Entidade { get; set; } = string.Empty;
    public int EntidadeId { get; set; }
    public string? DadosAntes { get; set; }
    public string? DadosDepois { get; set; }
    public string? Motivo { get; set; }
    public DateTime DataAcao { get; set; }
}
