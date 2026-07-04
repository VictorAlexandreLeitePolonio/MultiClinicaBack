using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class RecebimentoService(
    IRecebimentoRepository recebimentoRepository,
    IContaReceberRepository contaReceberRepository,
    IUsuarioLogadoService usuario) : IRecebimentoService
{
    private static RecebimentoResponseDto Map(Recebimento r) => new()
    {
        Id = r.Id,
        ContaReceberId = r.ContaReceberId,
        ContaFinanceiraId = r.ContaFinanceiraId,
        FormaPagamentoId = r.FormaPagamentoId,
        Valor = r.Valor,
        DataRecebimento = r.DataRecebimento,
        Observacao = r.Observacao,
        IsEstornado = r.IsEstornado,
        CreatedAt = r.CreatedAt
    };

    public async Task<Result<RecebimentoResponseDto>> RegistrarAsync(CreateRecebimentoDto dto)
    {
        if (dto.Valor <= 0)
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor do recebimento deve ser maior que zero.");
        if (!await recebimentoRepository.ContaFinanceiraExistsAsync(dto.ContaFinanceiraId))
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.NotFound, "Conta financeira não encontrada.");
        if (!await recebimentoRepository.FormaPagamentoExistsAsync(dto.FormaPagamentoId))
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.NotFound, "Forma de pagamento não encontrada.");

        var conta = await contaReceberRepository.GetByIdAsync(dto.ContaReceberId);
        if (conta is null)
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.NotFound, "Conta a receber não encontrada.");
        if (conta.Status == StatusContaReceber.Cancelada)
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.CannotModify, "Conta cancelada não pode receber pagamento.");

        var saldoRestante = conta.ValorTotal - conta.ValorRecebido;
        if (dto.Valor > saldoRestante)
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor recebido não pode exceder o saldo restante.");

        var recebimento = new Recebimento
        {
            ClinicaId = usuario.ClinicaId,
            ContaReceberId = dto.ContaReceberId,
            ContaFinanceiraId = dto.ContaFinanceiraId,
            FormaPagamentoId = dto.FormaPagamentoId,
            Valor = dto.Valor,
            DataRecebimento = dto.DataRecebimento,
            Observacao = dto.Observacao,
            CreatedByUserId = usuario.UserId
        };

        conta.Recebimentos.Add(recebimento);
        conta.ValorRecebido += dto.Valor;
        conta.Status = conta.ValorRecebido >= conta.ValorTotal
            ? StatusContaReceber.Paga
            : StatusContaReceber.Parcial;
        conta.DataPagamento = conta.Status == StatusContaReceber.Paga ? dto.DataRecebimento : null;
        conta.UpdatedByUserId = usuario.UserId;

        await recebimentoRepository.AddMovimentacaoAsync(new MovimentacaoFinanceira
        {
            ClinicaId = usuario.ClinicaId,
            ContaFinanceiraId = dto.ContaFinanceiraId,
            CategoriaFinanceiraId = conta.CategoriaFinanceiraId,
            ContaReceberId = conta.Id,
            Tipo = TipoMovimentacaoFinanceira.Entrada,
            Origem = OrigemMovimentacaoFinanceira.Recebimento,
            Descricao = $"Recebimento de {conta.Descricao}",
            Valor = dto.Valor,
            DataMovimentacao = dto.DataRecebimento,
            CreatedByUserId = usuario.UserId
        });
        await recebimentoRepository.SaveChangesAsync();

        return Result<RecebimentoResponseDto>.Ok(Map(recebimento));
    }

    public async Task<Result<RecebimentoResponseDto>> EstornarAsync(int id, EstornarRecebimentoDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do estorno é obrigatório.");

        var recebimento = await recebimentoRepository.GetByIdAsync(id);
        if (recebimento is null)
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.NotFound, "Recebimento não encontrado.");
        if (recebimento.IsEstornado)
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.CannotModify, "Recebimento já estornado.");

        var conta = await contaReceberRepository.GetByIdAsync(recebimento.ContaReceberId);
        if (conta is null)
            return Result<RecebimentoResponseDto>.Fail(ErrorCodes.NotFound, "Conta a receber não encontrada.");

        recebimento.IsEstornado = true;
        recebimento.MotivoEstorno = motivo;
        recebimento.UpdatedByUserId = usuario.UserId;

        conta.ValorRecebido = Math.Max(0, conta.ValorRecebido - recebimento.Valor);
        conta.Status = conta.ValorRecebido switch
        {
            0 => StatusContaReceber.Aberta,
            var v when v >= conta.ValorTotal => StatusContaReceber.Paga,
            _ => StatusContaReceber.Parcial
        };
        conta.DataPagamento = conta.Status == StatusContaReceber.Paga ? conta.DataPagamento : null;
        conta.UpdatedByUserId = usuario.UserId;

        await recebimentoRepository.AddMovimentacaoAsync(new MovimentacaoFinanceira
        {
            ClinicaId = usuario.ClinicaId,
            ContaFinanceiraId = recebimento.ContaFinanceiraId,
            CategoriaFinanceiraId = conta.CategoriaFinanceiraId,
            ContaReceberId = conta.Id,
            Tipo = TipoMovimentacaoFinanceira.Saida,
            Origem = OrigemMovimentacaoFinanceira.Estorno,
            Descricao = $"Estorno de {conta.Descricao}",
            Valor = recebimento.Valor,
            DataMovimentacao = DateTime.UtcNow,
            CreatedByUserId = usuario.UserId
        });
        await recebimentoRepository.SaveChangesAsync();

        return Result<RecebimentoResponseDto>.Ok(Map(recebimento));
    }
}
