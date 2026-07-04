using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IMovimentacaoFinanceiraRepository
{
    Task<List<MovimentacaoFinanceira>> GetByContaFinanceiraAndPeriodoAsync(int contaFinanceiraId, DateTime inicio, DateTime fim);
}
