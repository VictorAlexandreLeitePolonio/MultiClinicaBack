using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class CategoriaProdutoService(ICategoriaProdutoRepository repository, IUsuarioLogadoService usuario)
    : ICategoriaProdutoService
{
    private static CategoriaProdutoResponseDto Map(CategoriaProduto c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt
    };

    public async Task<Result<PagedResult<CategoriaProdutoResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(nome, page, pageSize);
        return Result<PagedResult<CategoriaProdutoResponseDto>>.Ok(new PagedResult<CategoriaProdutoResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<CategoriaProdutoResponseDto>> GetByIdAsync(int id)
    {
        var categoria = await repository.GetByIdAsync(id);
        return categoria is null
            ? Result<CategoriaProdutoResponseDto>.Fail(ErrorCodes.NotFound, "Categoria de produto não encontrada.")
            : Result<CategoriaProdutoResponseDto>.Ok(Map(categoria));
    }

    public async Task<Result<CategoriaProdutoResponseDto>> CreateAsync(CreateCategoriaProdutoDto dto)
    {
        var nome = dto.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return Result<CategoriaProdutoResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (await repository.ExistsActiveByNameAsync(nome, null))
            return Result<CategoriaProdutoResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe categoria de produto ativa com esse nome.");

        var entity = new CategoriaProduto
        {
            ClinicaId = usuario.ClinicaId,
            Nome = nome,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);
        return Result<CategoriaProdutoResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<CategoriaProdutoResponseDto>> UpdateAsync(int id, UpdateCategoriaProdutoDto dto)
    {
        var nome = dto.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return Result<CategoriaProdutoResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CategoriaProdutoResponseDto>.Fail(ErrorCodes.NotFound, "Categoria de produto não encontrada.");
        if (await repository.ExistsActiveByNameAsync(nome, id))
            return Result<CategoriaProdutoResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe categoria de produto ativa com esse nome.");

        entity.Nome = nome;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<CategoriaProdutoResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<CategoriaProdutoResponseDto>> SetActiveAsync(int id, bool active)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CategoriaProdutoResponseDto>.Fail(ErrorCodes.NotFound, "Categoria de produto não encontrada.");

        entity.IsActive = active;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<CategoriaProdutoResponseDto>.Ok(Map(entity));
    }
}
