using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Repositories;

public class RecebimentoRepository(AppDbContext db, IUsuarioLogadoService usuario) : IRecebimentoRepository
{
    public Task<Recebimento?> GetByIdAsync(int id) =>
        db.Recebimentos.FirstOrDefaultAsync(r => r.Id == id && r.ClinicaId == usuario.ClinicaId && !r.IsDeleted);

    public Task<bool> ContaFinanceiraExistsAsync(int id) =>
        db.ContasFinanceiras.AnyAsync(c => c.Id == id && c.ClinicaId == usuario.ClinicaId && !c.IsDeleted && c.IsActive);

    public Task<bool> FormaPagamentoExistsAsync(int id) =>
        db.FormasPagamento.AnyAsync(f => f.Id == id && f.ClinicaId == usuario.ClinicaId && !f.IsDeleted && f.IsActive);

    public Task AddMovimentacaoAsync(MovimentacaoFinanceira movimentacao)
    {
        db.MovimentacoesFinanceiras.Add(movimentacao);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
