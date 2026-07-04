using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ContaFinanceiraService(IContaFinanceiraRepository repository, IUsuarioLogadoService usuario)
    : IContaFinanceiraService
{
    private static ContaFinanceiraResponseDto Map(ContaFinanceira c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        Tipo = c.Tipo,
        SaldoInicial = c.SaldoInicial,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt
    };

    public async Task<Result<PagedResult<ContaFinanceiraResponseDto>>> GetPagedAsync(string? nome, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(nome, page, pageSize);
        return Result<PagedResult<ContaFinanceiraResponseDto>>.Ok(new PagedResult<ContaFinanceiraResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<ContaFinanceiraResponseDto>> GetByIdAsync(int id)
    {
        var conta = await repository.GetByIdAsync(id);
        return conta is null
            ? Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.NotFound, "Conta financeira não encontrada.")
            : Result<ContaFinanceiraResponseDto>.Ok(Map(conta));
    }

    public async Task<Result<ContaFinanceiraResponseDto>> CreateAsync(CreateContaFinanceiraDto dto)
    {
        var nome = dto.Nome.Trim();
        var validation = await ValidateAsync(nome, dto.Tipo, dto.SaldoInicial, null);
        if (validation is not null)
            return validation;

        var entity = new ContaFinanceira
        {
            ClinicaId = usuario.ClinicaId,
            Nome = nome,
            Tipo = dto.Tipo,
            SaldoInicial = dto.SaldoInicial,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);
        return Result<ContaFinanceiraResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<ContaFinanceiraResponseDto>> UpdateAsync(int id, UpdateContaFinanceiraDto dto)
    {
        var nome = dto.Nome.Trim();
        if (string.IsNullOrWhiteSpace(nome))
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (!Enum.IsDefined(dto.Tipo))
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.InvalidValue, "Tipo inválido.");
        if (dto.SaldoInicial < 0)
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.InvalidValue, "Saldo inicial não pode ser negativo.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.NotFound, "Conta financeira não encontrada.");
        if (await repository.ExistsActiveByNameAsync(nome, id))
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe conta ativa com esse nome.");

        entity.Nome = nome;
        entity.Tipo = dto.Tipo;
        entity.SaldoInicial = dto.SaldoInicial;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<ContaFinanceiraResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<ContaFinanceiraResponseDto>> SetActiveAsync(int id, bool active)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.NotFound, "Conta financeira não encontrada.");

        entity.IsActive = active;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<ContaFinanceiraResponseDto>.Ok(Map(entity));
    }

    private async Task<Result<ContaFinanceiraResponseDto>?> ValidateAsync(
        string nome,
        TipoContaFinanceira tipo,
        decimal saldoInicial,
        int? excludeId)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (!Enum.IsDefined(tipo))
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.InvalidValue, "Tipo inválido.");
        if (saldoInicial < 0)
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.InvalidValue, "Saldo inicial não pode ser negativo.");
        if (await repository.ExistsActiveByNameAsync(nome, excludeId))
            return Result<ContaFinanceiraResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe conta ativa com esse nome.");

        return null;
    }
}
