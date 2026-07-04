namespace MultiClinica.API.DTOs.Financial;

public class AjustarCaixaDto
{
    public decimal SaldoFinalInformado { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
