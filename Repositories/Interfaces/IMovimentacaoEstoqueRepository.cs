using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IMovimentacaoEstoqueRepository
{
    Task<(List<MovimentacaoEstoque> Items, int TotalCount)> GetPagedAsync(
        int? produtoId,
        TipoMovimentacaoEstoque? tipo,
        int page,
        int pageSize);
    Task<MovimentacaoEstoque?> GetByIdAsync(int id);
    Task<Produto?> GetProdutoAsync(int produtoId);
    Task<MovimentacaoEstoque> AddAsync(MovimentacaoEstoque entity);
    Task SaveChangesAsync();
}
