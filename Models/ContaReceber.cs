namespace MultiClinica.API.Models;

public enum StatusContaReceber
{
    Aberta,
    Parcial,
    Paga,
    Cancelada
}

public enum OrigemContaReceber
{
    Manual,
    Atendimento,
    Pacote,
    Produto,
    Convenio
}

public class ContaReceber : AuditableEntity
{
    public int ClinicaId { get; set; }
    public int PacienteId { get; set; }
    public int? CategoriaFinanceiraId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorOriginal { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorJuros { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorRecebido { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime DataVencimento { get; set; }
    public DateTime? DataPagamento { get; set; }
    public StatusContaReceber Status { get; set; } = StatusContaReceber.Aberta;
    public OrigemContaReceber Origem { get; set; } = OrigemContaReceber.Manual;
    public int? OrigemId { get; set; }
    public string? Observacao { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public Patient Paciente { get; set; } = null!;
    public CategoriaFinanceira? CategoriaFinanceira { get; set; }
    public ICollection<Recebimento> Recebimentos { get; set; } = [];
}
