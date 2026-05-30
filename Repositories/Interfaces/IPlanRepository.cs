using ProjetoLP.API.Models;

namespace ProjetoLP.API.Repositories.Interfaces;

public interface IPlanRepository
{
    /// <summary>Retorna planos paginados com filtros opcionais.</summary>
    Task<(List<Plans> Items, int TotalCount)> GetPagedAsync(
        TipoPlano? tipoPlano,
        TipoSessao? tipoSessao,
        bool? isActive,
        int page,
        int pageSize);

    /// <summary>Busca um plano pelo Id.</summary>
    Task<Plans?> GetByIdAsync(int id);

    /// <summary>Verifica se já existe plano com o mesmo nome.</summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null);

    /// <summary>Verifica se o plano tem pagamentos associados.</summary>
    Task<bool> HasPaymentsAsync(int id);

    /// <summary>Adiciona e salva um novo plano.</summary>
    Task<Plans> AddAsync(Plans plan);

    /// <summary>Salva alterações em um plano já rastreado.</summary>
    Task SaveChangesAsync();

    /// <summary>Remove um plano já rastreado.</summary>
    Task DeleteAsync(Plans plan);
}
