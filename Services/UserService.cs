using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.User;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class UserService(IUserRepository repository, IUsuarioLogadoService usuario) : IUserService
{
    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<UserResponseDto>>> GetPagedAsync(int page, int pageSize)
    {
        var (items, totalCount) = await repository.GetPagedAsync(page, pageSize);

        var data = items.Select(u => new UserResponseDto
        {
            Id        = u.Id,
            Name      = u.Name,
            Email     = u.Email,
            Role      = u.Role,
            CreatedAt = u.CreatedAt,
        }).ToList();

        return Result<PagedResult<UserResponseDto>>.Ok(new PagedResult<UserResponseDto>
        {
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
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

        if (!usuario.IsSuperAdmin && dto.Role == UserRole.SuperAdmin)
            return Result<UserResponseDto>.Fail(
                ErrorCodes.Forbidden, "Administrador não pode criar SuperAdmin.");

        if (!usuario.IsSuperAdmin && dto.Role == UserRole.Administrador)
            return Result<UserResponseDto>.Fail(
                ErrorCodes.Forbidden, "Administrador só pode criar Profissional ou Recepcao.");

        var user = new User
        {
            ClinicaId     = usuario.ClinicaId,
            Name         = dto.Name,
            Email        = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role         = dto.Role,
            CreatedByUserId = usuario.UserId
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
        user.Email     = dto.Email.Trim().ToLowerInvariant();
        user.UpdatedByUserId = usuario.UserId;
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

        // Impede deletar o último Administrador
        if (user.Role == UserRole.Administrador)
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
