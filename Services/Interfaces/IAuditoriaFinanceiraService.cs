namespace MultiClinica.API.Services.Interfaces;

public interface IAuditoriaFinanceiraService
{
    Task RegistrarAsync(
        string modulo,
        string acao,
        string entidade,
        int entidadeId,
        object? dadosAntes,
        object? dadosDepois,
        string? motivo = null);
}
