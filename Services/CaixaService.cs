using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class CaixaService(
    ICaixaRepository repository,
    IMovimentacaoFinanceiraRepository movimentacaoRepository,
    IAuditoriaFinanceiraService auditoria,
    IUsuarioLogadoService usuario) : ICaixaService
{
    private static CaixaResponseDto Map(Caixa c) => new()
    {
        Id = c.Id,
        ContaFinanceiraId = c.ContaFinanceiraId,
        DataAbertura = c.DataAbertura,
        DataFechamento = c.DataFechamento,
        SaldoInicial = c.SaldoInicial,
        SaldoFinalInformado = c.SaldoFinalInformado,
        SaldoFinalCalculado = c.SaldoFinalCalculado,
        Diferenca = c.Diferenca,
        Status = c.Status,
        Observacao = c.Observacao,
        CreatedAt = c.CreatedAt
    };

    public async Task<Result<PagedResult<CaixaResponseDto>>> GetPagedAsync(StatusCaixa? status, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(status, page, pageSize);
        return Result<PagedResult<CaixaResponseDto>>.Ok(new PagedResult<CaixaResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<CaixaResponseDto>> GetAtualAsync()
    {
        var caixa = await repository.GetAbertoAsync();
        return caixa is null
            ? Result<CaixaResponseDto>.Fail(ErrorCodes.NotFound, "Nenhum caixa aberto no momento.")
            : Result<CaixaResponseDto>.Ok(Map(caixa));
    }

    public async Task<Result<CaixaResponseDto>> GetByIdAsync(int id)
    {
        var caixa = await repository.GetByIdAsync(id);
        return caixa is null
            ? Result<CaixaResponseDto>.Fail(ErrorCodes.NotFound, "Caixa não encontrado.")
            : Result<CaixaResponseDto>.Ok(Map(caixa));
    }

    public async Task<Result<List<MovimentacaoResumoDto>>> GetMovimentacoesAsync(int id)
    {
        var caixa = await repository.GetByIdAsync(id);
        if (caixa is null)
            return Result<List<MovimentacaoResumoDto>>.Fail(ErrorCodes.NotFound, "Caixa não encontrado.");

        var fim = caixa.DataFechamento ?? DateTime.UtcNow;
        var movimentacoes = await movimentacaoRepository.GetByContaFinanceiraAndPeriodoAsync(
            caixa.ContaFinanceiraId, caixa.DataAbertura, fim);

        return Result<List<MovimentacaoResumoDto>>.Ok(movimentacoes.Select(m => new MovimentacaoResumoDto
        {
            Id = m.Id,
            Tipo = m.Tipo.ToString(),
            Origem = m.Origem.ToString(),
            Descricao = m.Descricao,
            Valor = m.Valor,
            DataMovimentacao = m.DataMovimentacao
        }).ToList());
    }

    public async Task<Result<CaixaResponseDto>> AbrirAsync(AbrirCaixaDto dto)
    {
        if (dto.SaldoInicial < 0)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.InvalidValue, "O saldo inicial não pode ser negativo.");
        if (!await repository.ContaFinanceiraExistsAsync(dto.ContaFinanceiraId))
            return Result<CaixaResponseDto>.Fail(ErrorCodes.NotFound, "Conta financeira não encontrada.");
        if (await repository.GetAbertoAsync() is not null)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.AlreadyOpen, "Já existe um caixa aberto para esta clínica.");

        var entity = new Caixa
        {
            ClinicaId = usuario.ClinicaId,
            ContaFinanceiraId = dto.ContaFinanceiraId,
            UsuarioAberturaId = usuario.UserId,
            DataAbertura = DateTime.UtcNow,
            SaldoInicial = dto.SaldoInicial,
            Observacao = dto.Observacao,
            Status = StatusCaixa.Aberto,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Caixa", "AbrirCaixa", "Caixa", entity.Id, null, depois);

        return Result<CaixaResponseDto>.Ok(depois);
    }

    public async Task<Result<CaixaResponseDto>> FecharAsync(int id, FecharCaixaDto dto)
    {
        var caixa = await repository.GetByIdAsync(id);
        if (caixa is null)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.NotFound, "Caixa não encontrado.");
        if (caixa.Status != StatusCaixa.Aberto)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.CannotModify, "Só é possível fechar um caixa aberto.");

        var agora = DateTime.UtcNow;
        var movimentacoes = await movimentacaoRepository.GetByContaFinanceiraAndPeriodoAsync(
            caixa.ContaFinanceiraId, caixa.DataAbertura, agora);
        var entradas = movimentacoes.Where(m => m.Tipo == TipoMovimentacaoFinanceira.Entrada).Sum(m => m.Valor);
        var saidas = movimentacoes.Where(m => m.Tipo == TipoMovimentacaoFinanceira.Saida).Sum(m => m.Valor);

        var antes = Map(caixa);

        caixa.SaldoFinalInformado = dto.SaldoFinalInformado;
        caixa.SaldoFinalCalculado = caixa.SaldoInicial + entradas - saidas;
        caixa.Diferenca = dto.SaldoFinalInformado - caixa.SaldoFinalCalculado;
        caixa.Status = StatusCaixa.Fechado;
        caixa.DataFechamento = agora;
        caixa.UsuarioFechamentoId = usuario.UserId;
        caixa.Observacao = dto.Observacao ?? caixa.Observacao;
        caixa.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();

        var depois = Map(caixa);
        await auditoria.RegistrarAsync("Caixa", "FecharCaixa", "Caixa", caixa.Id, antes, depois);

        return Result<CaixaResponseDto>.Ok(depois);
    }

    public async Task<Result<CaixaResponseDto>> ReabrirAsync(int id, MotivoDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<CaixaResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo da reabertura é obrigatório.");

        var caixa = await repository.GetByIdAsync(id);
        if (caixa is null)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.NotFound, "Caixa não encontrado.");
        if (caixa.Status != StatusCaixa.Fechado)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.CannotModify, "Só é possível reabrir um caixa fechado.");

        var antes = Map(caixa);

        caixa.Status = StatusCaixa.Aberto;
        caixa.MotivoReabertura = motivo;
        caixa.DataFechamento = null;
        caixa.SaldoFinalInformado = null;
        caixa.SaldoFinalCalculado = null;
        caixa.Diferenca = null;
        caixa.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();

        var depois = Map(caixa);
        await auditoria.RegistrarAsync("Caixa", "ReabrirCaixa", "Caixa", caixa.Id, antes, depois, motivo);

        return Result<CaixaResponseDto>.Ok(depois);
    }

    public async Task<Result<CaixaResponseDto>> AjustarAsync(int id, AjustarCaixaDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<CaixaResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do ajuste é obrigatório.");

        var caixa = await repository.GetByIdAsync(id);
        if (caixa is null)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.NotFound, "Caixa não encontrado.");
        if (caixa.Status != StatusCaixa.Fechado)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.CannotModify, "Só é possível ajustar um caixa fechado.");

        var antes = Map(caixa);

        caixa.SaldoFinalInformado = dto.SaldoFinalInformado;
        caixa.Diferenca = dto.SaldoFinalInformado - caixa.SaldoFinalCalculado;
        caixa.MotivoAjuste = motivo;
        caixa.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();

        var depois = Map(caixa);
        await auditoria.RegistrarAsync("Caixa", "AjustarCaixa", "Caixa", caixa.Id, antes, depois, motivo);

        return Result<CaixaResponseDto>.Ok(depois);
    }

    public async Task<Result<CaixaResponseDto>> CancelarAsync(int id, MotivoDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<CaixaResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do cancelamento é obrigatório.");

        var caixa = await repository.GetByIdAsync(id);
        if (caixa is null)
            return Result<CaixaResponseDto>.Fail(ErrorCodes.NotFound, "Caixa não encontrado.");

        var antes = Map(caixa);

        caixa.Status = StatusCaixa.Cancelado;
        caixa.MotivoCancelamento = motivo;
        caixa.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();

        var depois = Map(caixa);
        await auditoria.RegistrarAsync("Caixa", "CancelarCaixa", "Caixa", caixa.Id, antes, depois, motivo);

        return Result<CaixaResponseDto>.Ok(depois);
    }
}
