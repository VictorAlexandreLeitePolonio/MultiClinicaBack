using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class FornecedorService(
    IFornecedorRepository repository,
    IAuditoriaFinanceiraService auditoria,
    IUsuarioLogadoService usuario) : IFornecedorService
{
    private static FornecedorResponseDto Map(Fornecedor f) => new()
    {
        Id = f.Id,
        Nome = f.Nome,
        IsActive = f.IsActive,
        CreatedAt = f.CreatedAt
    };

    public async Task<Result<PagedResult<FornecedorResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(nome, page, pageSize);
        return Result<PagedResult<FornecedorResponseDto>>.Ok(new PagedResult<FornecedorResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<FornecedorResponseDto>> GetByIdAsync(int id)
    {
        var fornecedor = await repository.GetByIdAsync(id);
        return fornecedor is null
            ? Result<FornecedorResponseDto>.Fail(ErrorCodes.NotFound, "Fornecedor não encontrado.")
            : Result<FornecedorResponseDto>.Ok(Map(fornecedor));
    }

    public async Task<Result<FornecedorResponseDto>> CreateAsync(CreateFornecedorDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return Result<FornecedorResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");

        var entity = new Fornecedor
        {
            ClinicaId = usuario.ClinicaId,
            Nome = dto.Nome.Trim(),
            CreatedByUserId = usuario.UserId
        };
        await repository.AddAsync(entity);

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Fornecedores", "Criar", "Fornecedor", entity.Id, null, depois);

        return Result<FornecedorResponseDto>.Ok(depois);
    }

    public async Task<Result<FornecedorResponseDto>> UpdateAsync(int id, UpdateFornecedorDto dto)
    {
        var nome = dto.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return Result<FornecedorResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<FornecedorResponseDto>.Fail(ErrorCodes.NotFound, "Fornecedor não encontrado.");

        var antes = Map(entity);
        entity.Nome = nome;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Fornecedores", "Editar", "Fornecedor", entity.Id, antes, depois);
        return Result<FornecedorResponseDto>.Ok(depois);
    }

    public async Task<Result<FornecedorResponseDto>> SetActiveAsync(int id, bool active)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<FornecedorResponseDto>.Fail(ErrorCodes.NotFound, "Fornecedor não encontrado.");

        var antes = Map(entity);
        entity.IsActive = active;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync(
            "Fornecedores",
            active ? "Reativar" : "Inativar",
            "Fornecedor",
            entity.Id,
            antes,
            depois);
        return Result<FornecedorResponseDto>.Ok(depois);
    }
}
