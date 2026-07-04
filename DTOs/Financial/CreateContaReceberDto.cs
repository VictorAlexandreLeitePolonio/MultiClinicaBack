using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Financial;

public class CreateContaReceberDto
{
    public int PacienteId { get; set; }
    public int? CategoriaFinanceiraId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorOriginal { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorJuros { get; set; }
    public DateTime DataEmissao { get; set; }
    public DateTime DataVencimento { get; set; }
    public OrigemContaReceber Origem { get; set; } = OrigemContaReceber.Manual;
    public int? OrigemId { get; set; }
    public string? Observacao { get; set; }
}
