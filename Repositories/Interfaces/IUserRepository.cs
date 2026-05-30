using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IUserRepository
{
    /// <summary>Retorna usuários paginados.</summary>
    Task<(List<User> Items, int TotalCount)> GetPagedAsync(int page, int pageSize);

    /// <summary>Busca um usuário pelo Id.</summary>
    Task<User?> GetByIdAsync(int id);

    /// <summary>Busca um usuário pelo email.</summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>Verifica se o email já está cadastrado.</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeId = null);

    /// <summary>Conta quantos admins existem no sistema.</summary>
    Task<int> CountAdminsAsync();

    /// <summary>Verifica se o usuário tem registros associados.</summary>
    Task<bool> HasAssociatedRecordsAsync(int id);

    /// <summary>Adiciona e salva um novo usuário.</summary>
    Task<User> AddAsync(User user);

    /// <summary>Salva alterações em um usuário já rastreado.</summary>
    Task SaveChangesAsync();

    /// <summary>Remove um usuário já rastreado.</summary>
    Task DeleteAsync(User user);
}
