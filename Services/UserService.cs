using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs.User;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Services;

public class UserService(IUserRepository repository) : IUserService
{
    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<List<UserResponseDto>>> GetAllAsync()
    {
        var users = await repository.GetAllAsync();

        var data = users.Select(u => new UserResponseDto
        {
            Id        = u.Id,
            Name      = u.Name,
            Email     = u.Email,
            Role      = u.Role,
            CreatedAt = u.CreatedAt,
        }).ToList();

        return Result<List<UserResponseDto>>.Ok(data);
    }

    // ── Busca por Id ─────────────────────────────────────────────────────────

    public async Task<Result<UserResponseDto>> GetByIdAsync(int id)
    {
        var user = await repository.GetByIdAsync(id);
        if (user is null)
            return Result<UserResponseDto>.Fail(ErrorCodes.NotFound, "Usuário não encontrado.");

        return Result<UserResponseDto>.Ok(new UserResponseDto
        {
            Id        = user.Id,
            Name      = user.Name,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt,
        });
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<UserResponseDto>> CreateAsync(CreateUserDto dto)
    {
        // Senha deve ter no mínimo 6 caracteres
        if (dto.Password.Length < 6)
            return Result<UserResponseDto>.Fail(
                ErrorCodes.InvalidPassword, "A senha deve ter no mínimo 6 caracteres.");

        // Email deve ser único
        if (await repository.EmailExistsAsync(dto.Email))
            return Result<UserResponseDto>.Fail(
                ErrorCodes.DuplicateEmail, "Email já cadastrado por outro usuário.");

        var user = new User
        {
            Name         = dto.Name,
            Email        = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role         = dto.Role
        };

        await repository.AddAsync(user);

        return Result<UserResponseDto>.Ok(new UserResponseDto
        {
            Id        = user.Id,
            Name      = user.Name,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt,
        });
    }

    // ── Atualização ──────────────────────────────────────────────────────────

    public async Task<Result<UserResponseDto>> UpdateAsync(int id, UpdateUserDto dto)
    {
        // Email deve ser único entre outros usuários
        if (await repository.EmailExistsAsync(dto.Email, id))
            return Result<UserResponseDto>.Fail(
                ErrorCodes.DuplicateEmail, "Email já cadastrado por outro usuário.");

        var user = await repository.GetByIdAsync(id);
        if (user is null)
            return Result<UserResponseDto>.Fail(ErrorCodes.NotFound, "Usuário não encontrado.");

        user.Name      = dto.Name;
        user.Email     = dto.Email;
        user.UpdatedAt = DateTime.UtcNow;

        await repository.SaveChangesAsync();

        return Result<UserResponseDto>.Ok(new UserResponseDto
        {
            Id        = user.Id,
            Name      = user.Name,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt,
        });
    }

    // ── Deleção ──────────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var user = await repository.GetByIdAsync(id);
        if (user is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Usuário não encontrado.");

        // Impede deletar o último Admin
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await repository.CountAdminsAsync();
            if (adminCount <= 1)
                return Result<bool>.Fail(
                    ErrorCodes.LastAdmin, "Não é possível excluir o único administrador do sistema.");
        }

        // Impede exclusão se houver registros filhos
        if (await repository.HasAssociatedRecordsAsync(id))
            return Result<bool>.Fail(
                ErrorCodes.HasAssociatedRecords, "Não é possível excluir usuário com agendamentos ou prontuários associados.");

        await repository.DeleteAsync(user);
        return Result<bool>.Ok(true);
    }
}
