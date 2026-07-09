using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class RelatorioRepository(AppDbContext db, IUsuarioLogadoService usuario) : IRelatorioRepository
{
    public Task<List<Recebimento>> GetRecebimentosAsync(DateTime de, DateTime ate) =>
        db.Recebimentos
            .Include(r => r.FormaPagamento)
            .Include(r => r.ContaReceber)
                .ThenInclude(c => c.CategoriaFinanceira)
            .Where(r => r.ClinicaId == usuario.ClinicaId
                && r.DataRecebimento >= de
                && r.DataRecebimento <= ate
                && !r.IsEstornado
                && r.ContaReceber.Status != StatusContaReceber.Cancelada)
            .ToListAsync();

    public Task<List<PagamentoContaPagar>> GetPagamentosAsync(DateTime de, DateTime ate) =>
        db.PagamentosContaPagar
            .Include(p => p.ContaPagar)
                .ThenInclude(c => c.CategoriaFinanceira)
            .Where(p => p.ClinicaId == usuario.ClinicaId
                && p.DataPagamento >= de
                && p.DataPagamento <= ate
                && !p.IsEstornado
                && p.ContaPagar.Status != StatusContaPagar.Cancelada)
            .ToListAsync();

    public Task<List<MovimentacaoEstoque>> GetMovimentacoesEstoqueAsync(DateTime de, DateTime ate) =>
        db.MovimentacoesEstoque
            .Include(m => m.Produto)
            .Where(m => m.ClinicaId == usuario.ClinicaId
                && m.CreatedAt >= de
                && m.CreatedAt <= ate
                && !m.IsCancelada)
            .ToListAsync();
}
