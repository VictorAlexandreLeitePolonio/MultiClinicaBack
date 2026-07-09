using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ContaReceberService(
    IContaReceberRepository repository,
    IAuditoriaFinanceiraService auditoria,
    IUsuarioLogadoService usuario)
    : IContaReceberService
{
    private static ContaReceberResponseDto Map(ContaReceber c) => new()
    {
        Id = c.Id,
        PacienteId = c.PacienteId,
        CategoriaFinanceiraId = c.CategoriaFinanceiraId,
        Descricao = c.Descricao,
        ValorOriginal = c.ValorOriginal,
        ValorDesconto = c.ValorDesconto,
        ValorJuros = c.ValorJuros,
        ValorTotal = c.ValorTotal,
        ValorRecebido = c.ValorRecebido,
        DataEmissao = c.DataEmissao,
        DataVencimento = c.DataVencimento,
        DataPagamento = c.DataPagamento,
        Status = c.Status,
        Vencida = (c.Status == StatusContaReceber.Aberta || c.Status == StatusContaReceber.Parcial)
            && c.DataVencimento < DateTime.UtcNow,
        Origem = c.Origem,
        Observacao = c.Observacao,
        CreatedAt = c.CreatedAt
    };

    public async Task<Result<PagedResult<ContaReceberResponseDto>>> GetPagedAsync(
        int? pacienteId,
        StatusContaReceber? status,
        int page,
        int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(pacienteId, status, page, pageSize);
        return Result<PagedResult<ContaReceberResponseDto>>.Ok(new PagedResult<ContaReceberResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<ContaReceberResponseDto>> GetByIdAsync(int id)
    {
        var conta = await repository.GetByIdAsync(id);
        return conta is null
            ? Result<ContaReceberResponseDto>.Fail(ErrorCodes.NotFound, "Conta a receber não encontrada.")
            : Result<ContaReceberResponseDto>.Ok(Map(conta));
    }

    public async Task<Result<List<ContaReceberResponseDto>>> GetInadimplentesAsync()
    {
        var items = await repository.GetInadimplentesAsync();
        return Result<List<ContaReceberResponseDto>>.Ok(items.Select(Map).ToList());
    }

    public async Task<Result<ContaReceberResponseDto>> CreateAsync(CreateContaReceberDto dto)
    {
        var validation = await ValidateAsync(dto.PacienteId, dto.CategoriaFinanceiraId, dto.Descricao, dto.ValorOriginal);
        if (validation is not null)
            return validation;

        var desconto = usuario.Role == UserRole.Recepcao ? 0 : dto.ValorDesconto;
        var juros = usuario.Role == UserRole.Recepcao ? 0 : dto.ValorJuros;

        var entity = new ContaReceber
        {
            ClinicaId = usuario.ClinicaId,
            PacienteId = dto.PacienteId,
            CategoriaFinanceiraId = dto.CategoriaFinanceiraId,
            Descricao = dto.Descricao.Trim(),
            ValorOriginal = dto.ValorOriginal,
            ValorDesconto = desconto,
            ValorJuros = juros,
            ValorTotal = dto.ValorOriginal - desconto + juros,
            DataEmissao = dto.DataEmissao,
            DataVencimento = dto.DataVencimento,
            Origem = dto.Origem,
            OrigemId = dto.OrigemId,
            Observacao = dto.Observacao,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);

        var depois = Map(entity);
        await auditoria.RegistrarAsync("ContasReceber", "Criar", "ContaReceber", entity.Id, null, depois);

        return Result<ContaReceberResponseDto>.Ok(depois);
    }

    public async Task<Result<ContaReceberResponseDto>> UpdateAsync(int id, UpdateContaReceberDto dto)
    {
        var validation = await ValidateAsync(null, dto.CategoriaFinanceiraId, dto.Descricao, dto.ValorOriginal);
        if (validation is not null)
            return validation;

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.NotFound, "Conta a receber não encontrada.");
        if (entity.Status == StatusContaReceber.Paga)
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.CannotModify, "Conta já paga não pode ser editada.");

        var antes = Map(entity);

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

        var depois = Map(entity);
        await auditoria.RegistrarAsync("ContasReceber", "Editar", "ContaReceber", entity.Id, antes, depois);

        return Result<ContaReceberResponseDto>.Ok(depois);
    }

    public async Task<Result<ContaReceberResponseDto>> CancelarAsync(int id, MotivoDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do cancelamento é obrigatório.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.NotFound, "Conta a receber não encontrada.");
        if (entity.ValorRecebido > 0)
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.CannotModify, "Estorne os recebimentos antes de cancelar a conta.");

        var antes = Map(entity);

        entity.Status = StatusContaReceber.Cancelada;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync("ContasReceber", "Cancelar", "ContaReceber", entity.Id, antes, depois, motivo);

        return Result<ContaReceberResponseDto>.Ok(depois);
    }

    private async Task<Result<ContaReceberResponseDto>?> ValidateAsync(
        int? pacienteId,
        int? categoriaId,
        string descricao,
        decimal valorOriginal)
    {
        if (valorOriginal <= 0)
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor original deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(descricao))
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.EmptyField, "A descrição é obrigatória.");
        if (pacienteId is not null && !await repository.PatientExistsAsync(pacienteId.Value))
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");
        if (categoriaId is not null && !await repository.CategoriaExistsAsync(categoriaId.Value))
            return Result<ContaReceberResponseDto>.Fail(ErrorCodes.NotFound, "Categoria não encontrada.");

        return null;
    }
}
