using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class MovimentacaoFinanceiraRepository(AppDbContext db, IUsuarioLogadoService usuario)
    : IMovimentacaoFinanceiraRepository
{
    public Task<List<MovimentacaoFinanceira>> GetByContaFinanceiraAndPeriodoAsync(int contaFinanceiraId, DateTime inicio, DateTime fim) =>
        db.MovimentacoesFinanceiras
            .Where(m => m.ClinicaId == usuario.ClinicaId
                && !m.IsDeleted
                && m.ContaFinanceiraId == contaFinanceiraId
                && m.DataMovimentacao >= inicio
                && m.DataMovimentacao <= fim)
            .OrderBy(m => m.DataMovimentacao)
            .ToListAsync();
}
