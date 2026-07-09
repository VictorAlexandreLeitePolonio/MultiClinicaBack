using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ContaPagarService(
    IContaPagarRepository repository,
    IAuditoriaFinanceiraService auditoria,
    IUsuarioLogadoService usuario) : IContaPagarService
{
    private static ContaPagarResponseDto Map(ContaPagar c) => new()
    {
        Id = c.Id,
        FornecedorId = c.FornecedorId,
        CategoriaFinanceiraId = c.CategoriaFinanceiraId,
        Descricao = c.Descricao,
        ValorOriginal = c.ValorOriginal,
        ValorDesconto = c.ValorDesconto,
        ValorJuros = c.ValorJuros,
        ValorTotal = c.ValorTotal,
        ValorPago = c.ValorPago,
        DataEmissao = c.DataEmissao,
        DataVencimento = c.DataVencimento,
        DataPagamento = c.DataPagamento,
        Status = c.Status,
        Vencida = (c.Status == StatusContaPagar.Aberta || c.Status == StatusContaPagar.Parcial)
            && c.DataVencimento < DateTime.UtcNow,
        Origem = c.Origem,
        Observacao = c.Observacao,
        CreatedAt = c.CreatedAt
    };

    public async Task<Result<PagedResult<ContaPagarResponseDto>>> GetPagedAsync(
        int? fornecedorId,
        StatusContaPagar? status,
        int page,
        int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(fornecedorId, status, page, pageSize);
        return Result<PagedResult<ContaPagarResponseDto>>.Ok(new PagedResult<ContaPagarResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<ContaPagarResponseDto>> GetByIdAsync(int id)
    {
        var conta = await repository.GetByIdAsync(id);
        return conta is null
            ? Result<ContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Conta a pagar não encontrada.")
            : Result<ContaPagarResponseDto>.Ok(Map(conta));
    }

    public async Task<Result<ContaPagarResponseDto>> CreateAsync(CreateContaPagarDto dto)
    {
        var validation = await ValidateAsync(dto.FornecedorId, dto.CategoriaFinanceiraId, dto.Descricao, dto.ValorOriginal);
        if (validation is not null)
            return validation;

        var entity = new ContaPagar
        {
            ClinicaId = usuario.ClinicaId,
            FornecedorId = dto.FornecedorId,
            CategoriaFinanceiraId = dto.CategoriaFinanceiraId,
            Descricao = dto.Descricao.Trim(),
            ValorOriginal = dto.ValorOriginal,
            ValorDesconto = dto.ValorDesconto,
            ValorJuros = dto.ValorJuros,
            ValorTotal = dto.ValorOriginal - dto.ValorDesconto + dto.ValorJuros,
            DataEmissao = dto.DataEmissao,
            DataVencimento = dto.DataVencimento,
            Origem = dto.Origem,
            OrigemId = dto.OrigemId,
            Observacao = dto.Observacao,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);

        var depois = Map(entity);
        await auditoria.RegistrarAsync("ContasPagar", "Criar", "ContaPagar", entity.Id, null, depois);

        return Result<ContaPagarResponseDto>.Ok(depois);
    }

    public async Task<Result<ContaPagarResponseDto>> UpdateAsync(int id, UpdateContaPagarDto dto)
    {
        var validation = await ValidateAsync(null, dto.CategoriaFinanceiraId, dto.Descricao, dto.ValorOriginal);
        if (validation is not null)
            return validation;

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Conta a pagar não encontrada.");
        if (entity.Status == StatusContaPagar.Paga)
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.CannotModify, "Conta já paga não pode ser editada.");

        entity.CategoriaFinanceiraId = dto.CategoriaFinanceiraId;
        entity.Descricao = dto.Descricao.Trim();
        entity.ValorOriginal = dto.ValorOriginal;
        entity.ValorDesconto = dto.ValorDesconto;
        entity.ValorJuros = dto.ValorJuros;
        entity.ValorTotal = dto.ValorOriginal - dto.ValorDesconto + dto.ValorJuros;
        entity.DataVencimento = dto.DataVencimento;
        entity.Observacao = dto.Observacao;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<ContaPagarResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<ContaPagarResponseDto>> CancelarAsync(int id, MotivoDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do cancelamento é obrigatório.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Conta a pagar não encontrada.");
        if (entity.ValorPago > 0)
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.CannotModify, "Estorne os pagamentos antes de cancelar a conta.");

        var antes = Map(entity);

        entity.Status = StatusContaPagar.Cancelada;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync("ContasPagar", "Cancelar", "ContaPagar", entity.Id, antes, depois, motivo);

        return Result<ContaPagarResponseDto>.Ok(depois);
    }

    private async Task<Result<ContaPagarResponseDto>?> ValidateAsync(int? fornecedorId, int? categoriaId, string descricao, decimal valorOriginal)
    {
        if (valorOriginal <= 0)
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor original deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(descricao))
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.EmptyField, "A descrição é obrigatória.");
        if (fornecedorId is not null && !await repository.FornecedorExistsAsync(fornecedorId.Value))
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Fornecedor não encontrado.");
        if (categoriaId is not null && !await repository.CategoriaExistsAsync(categoriaId.Value))
            return Result<ContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Categoria não encontrada.");

        return null;
    }
}
