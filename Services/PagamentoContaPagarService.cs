using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PagamentoContaPagarService(
    IPagamentoContaPagarRepository pagamentoRepository,
    IContaPagarRepository contaPagarRepository,
    IUsuarioLogadoService usuario) : IPagamentoContaPagarService
{
    private static PagamentoContaPagarResponseDto Map(PagamentoContaPagar p) => new()
    {
        Id = p.Id,
        ContaPagarId = p.ContaPagarId,
        ContaFinanceiraId = p.ContaFinanceiraId,
        FormaPagamentoId = p.FormaPagamentoId,
        Valor = p.Valor,
        DataPagamento = p.DataPagamento,
        Observacao = p.Observacao,
        IsEstornado = p.IsEstornado,
        CreatedAt = p.CreatedAt
    };

    public async Task<Result<PagamentoContaPagarResponseDto>> RegistrarAsync(CreatePagamentoContaPagarDto dto)
    {
        if (dto.Valor <= 0)
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor do pagamento deve ser maior que zero.");
        if (!await pagamentoRepository.ContaFinanceiraExistsAsync(dto.ContaFinanceiraId))
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Conta financeira não encontrada.");
        if (!await pagamentoRepository.FormaPagamentoExistsAsync(dto.FormaPagamentoId))
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Forma de pagamento não encontrada.");

        var conta = await contaPagarRepository.GetByIdAsync(dto.ContaPagarId);
        if (conta is null)
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Conta a pagar não encontrada.");
        if (conta.Status == StatusContaPagar.Cancelada)
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.CannotModify, "Conta cancelada não pode receber pagamento.");

        var saldoRestante = conta.ValorTotal - conta.ValorPago;
        if (dto.Valor > saldoRestante)
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor pago não pode exceder o saldo restante.");

        var pagamento = new PagamentoContaPagar
        {
            ClinicaId = usuario.ClinicaId,
            ContaPagarId = dto.ContaPagarId,
            ContaFinanceiraId = dto.ContaFinanceiraId,
            FormaPagamentoId = dto.FormaPagamentoId,
            Valor = dto.Valor,
            DataPagamento = dto.DataPagamento,
            Observacao = dto.Observacao,
            CreatedByUserId = usuario.UserId
        };

        conta.Pagamentos.Add(pagamento);
        conta.ValorPago += dto.Valor;
        conta.Status = conta.ValorPago >= conta.ValorTotal
            ? StatusContaPagar.Paga
            : StatusContaPagar.Parcial;
        conta.DataPagamento = conta.Status == StatusContaPagar.Paga ? dto.DataPagamento : null;
        conta.UpdatedByUserId = usuario.UserId;

        await pagamentoRepository.AddMovimentacaoAsync(new MovimentacaoFinanceira
        {
            ClinicaId = usuario.ClinicaId,
            ContaFinanceiraId = dto.ContaFinanceiraId,
            CategoriaFinanceiraId = conta.CategoriaFinanceiraId,
            Tipo = TipoMovimentacaoFinanceira.Saida,
            Origem = OrigemMovimentacaoFinanceira.Pagamento,
            Descricao = $"Pagamento de {conta.Descricao}",
            Valor = dto.Valor,
            DataMovimentacao = dto.DataPagamento,
            CreatedByUserId = usuario.UserId
        });
        await pagamentoRepository.SaveChangesAsync();

        return Result<PagamentoContaPagarResponseDto>.Ok(Map(pagamento));
    }

    public async Task<Result<PagamentoContaPagarResponseDto>> EstornarAsync(int id, EstornarPagamentoContaPagarDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do estorno é obrigatório.");

        var pagamento = await pagamentoRepository.GetByIdAsync(id);
        if (pagamento is null)
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Pagamento não encontrado.");
        if (pagamento.IsEstornado)
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.CannotModify, "Pagamento já estornado.");

        var conta = await contaPagarRepository.GetByIdAsync(pagamento.ContaPagarId);
        if (conta is null)
            return Result<PagamentoContaPagarResponseDto>.Fail(ErrorCodes.NotFound, "Conta a pagar não encontrada.");

        pagamento.IsEstornado = true;
        pagamento.MotivoEstorno = motivo;
        pagamento.UpdatedByUserId = usuario.UserId;

        conta.ValorPago = Math.Max(0, conta.ValorPago - pagamento.Valor);
        conta.Status = conta.ValorPago switch
        {
            0 => StatusContaPagar.Aberta,
            var v when v >= conta.ValorTotal => StatusContaPagar.Paga,
            _ => StatusContaPagar.Parcial
        };
        conta.DataPagamento = conta.Status == StatusContaPagar.Paga ? conta.DataPagamento : null;
        conta.UpdatedByUserId = usuario.UserId;

        await pagamentoRepository.AddMovimentacaoAsync(new MovimentacaoFinanceira
        {
            ClinicaId = usuario.ClinicaId,
            ContaFinanceiraId = pagamento.ContaFinanceiraId,
            CategoriaFinanceiraId = conta.CategoriaFinanceiraId,
            Tipo = TipoMovimentacaoFinanceira.Entrada,
            Origem = OrigemMovimentacaoFinanceira.Estorno,
            Descricao = $"Estorno de {conta.Descricao}",
            Valor = pagamento.Valor,
            DataMovimentacao = DateTime.UtcNow,
            CreatedByUserId = usuario.UserId
        });
        await pagamentoRepository.SaveChangesAsync();

        return Result<PagamentoContaPagarResponseDto>.Ok(Map(pagamento));
    }
}
