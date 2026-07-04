using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class FormaPagamentoService(IFormaPagamentoRepository repository, IUsuarioLogadoService usuario)
    : IFormaPagamentoService
{
    private static FormaPagamentoResponseDto Map(FormaPagamento f) => new()
    {
        Id = f.Id,
        Nome = f.Nome,
        IsActive = f.IsActive,
        CreatedAt = f.CreatedAt
    };

    public async Task<Result<PagedResult<FormaPagamentoResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(nome, page, pageSize);
        return Result<PagedResult<FormaPagamentoResponseDto>>.Ok(new PagedResult<FormaPagamentoResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<FormaPagamentoResponseDto>> GetByIdAsync(int id)
    {
        var formaPagamento = await repository.GetByIdAsync(id);
        return formaPagamento is null
            ? Result<FormaPagamentoResponseDto>.Fail(ErrorCodes.NotFound, "Forma de pagamento não encontrada.")
            : Result<FormaPagamentoResponseDto>.Ok(Map(formaPagamento));
    }

    public async Task<Result<FormaPagamentoResponseDto>> CreateAsync(CreateFormaPagamentoDto dto)
    {
        var nome = dto.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return Result<FormaPagamentoResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (await repository.ExistsActiveByNameAsync(nome, null))
            return Result<FormaPagamentoResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe forma de pagamento ativa com esse nome.");

        var entity = new FormaPagamento
        {
            ClinicaId = usuario.ClinicaId,
            Nome = nome,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);
        return Result<FormaPagamentoResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<FormaPagamentoResponseDto>> UpdateAsync(int id, UpdateFormaPagamentoDto dto)
    {
        var nome = dto.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return Result<FormaPagamentoResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<FormaPagamentoResponseDto>.Fail(ErrorCodes.NotFound, "Forma de pagamento não encontrada.");
        if (await repository.ExistsActiveByNameAsync(nome, id))
            return Result<FormaPagamentoResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe forma de pagamento ativa com esse nome.");

        entity.Nome = nome;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<FormaPagamentoResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<FormaPagamentoResponseDto>> SetActiveAsync(int id, bool active)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<FormaPagamentoResponseDto>.Fail(ErrorCodes.NotFound, "Forma de pagamento não encontrada.");

        entity.IsActive = active;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<FormaPagamentoResponseDto>.Ok(Map(entity));
    }
}
