using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class CategoriaFinanceiraService(ICategoriaFinanceiraRepository repository, IUsuarioLogadoService usuario)
    : ICategoriaFinanceiraService
{
    private static CategoriaFinanceiraResponseDto Map(CategoriaFinanceira c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        Tipo = c.Tipo,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt
    };

    public async Task<Result<PagedResult<CategoriaFinanceiraResponseDto>>> GetPagedAsync(
        string? nome,
        TipoCategoriaFinanceira? tipo,
        int page,
        int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(nome, tipo, page, pageSize);
        return Result<PagedResult<CategoriaFinanceiraResponseDto>>.Ok(new PagedResult<CategoriaFinanceiraResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<CategoriaFinanceiraResponseDto>> GetByIdAsync(int id)
    {
        var categoria = await repository.GetByIdAsync(id);
        return categoria is null
            ? Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.NotFound, "Categoria não encontrada.")
            : Result<CategoriaFinanceiraResponseDto>.Ok(Map(categoria));
    }

    public async Task<Result<CategoriaFinanceiraResponseDto>> CreateAsync(CreateCategoriaFinanceiraDto dto)
    {
        var nome = dto.Nome.Trim();
        var validation = await ValidateAsync(nome, dto.Tipo, null);
        if (validation is not null)
            return validation;

        var entity = new CategoriaFinanceira
        {
            ClinicaId = usuario.ClinicaId,
            Nome = nome,
            Tipo = dto.Tipo,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);
        return Result<CategoriaFinanceiraResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<CategoriaFinanceiraResponseDto>> UpdateAsync(int id, UpdateCategoriaFinanceiraDto dto)
    {
        var nome = dto.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (!Enum.IsDefined(dto.Tipo))
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.InvalidValue, "Tipo inválido.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.NotFound, "Categoria não encontrada.");
        if (await repository.ExistsActiveByNameAndTipoAsync(nome, dto.Tipo, id))
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe categoria ativa com esse nome e tipo.");

        entity.Nome = nome;
        entity.Tipo = dto.Tipo;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<CategoriaFinanceiraResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<CategoriaFinanceiraResponseDto>> SetActiveAsync(int id, bool active)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.NotFound, "Categoria não encontrada.");

        entity.IsActive = active;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<CategoriaFinanceiraResponseDto>.Ok(Map(entity));
    }

    private async Task<Result<CategoriaFinanceiraResponseDto>?> ValidateAsync(
        string nome,
        TipoCategoriaFinanceira tipo,
        int? excludeId)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (!Enum.IsDefined(tipo))
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.InvalidValue, "Tipo inválido.");
        if (await repository.ExistsActiveByNameAndTipoAsync(nome, tipo, excludeId))
            return Result<CategoriaFinanceiraResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe categoria ativa com esse nome e tipo.");

        return null;
    }
}
