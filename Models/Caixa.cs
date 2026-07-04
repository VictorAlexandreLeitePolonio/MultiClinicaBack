namespace MultiClinica.API.Models;

public enum StatusCaixa
{
    Aberto,
    Fechado,
    Cancelado
}

public class Caixa : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int ContaFinanceiraId { get; set; }
    public int? UsuarioAberturaId { get; set; }
    public int? UsuarioFechamentoId { get; set; }
    public DateTime DataAbertura { get; set; }
    public DateTime? DataFechamento { get; set; }
    public decimal SaldoInicial { get; set; }
    public decimal? SaldoFinalInformado { get; set; }
    public decimal? SaldoFinalCalculado { get; set; }
    public decimal? Diferenca { get; set; }
    public StatusCaixa Status { get; set; } = StatusCaixa.Aberto;
    public string? Observacao { get; set; }
    public string? MotivoReabertura { get; set; }
    public string? MotivoCancelamento { get; set; }
    public string? MotivoAjuste { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public ContaFinanceira ContaFinanceira { get; set; } = null!;
}
