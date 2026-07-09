using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class MovimentacaoEstoqueService(
    IMovimentacaoEstoqueRepository repository,
    IProdutoRepository produtoRepository,
    IAuditoriaFinanceiraService auditoria,
    IUsuarioLogadoService usuario) : IMovimentacaoEstoqueService
{
    private static MovimentacaoEstoqueResponseDto Map(MovimentacaoEstoque m) => new()
    {
        Id = m.Id,
        ProdutoId = m.ProdutoId,
        Tipo = m.Tipo,
        Quantidade = m.Quantidade,
        QuantidadeAnterior = m.QuantidadeAnterior,
        QuantidadeAtual = m.QuantidadeAtual,
        Origem = m.Origem,
        OrigemId = m.OrigemId,
        Observacao = m.Observacao,
        IsCancelada = m.IsCancelada,
        MotivoCancelamento = m.MotivoCancelamento,
        CreatedAt = m.CreatedAt
    };

    public async Task<Result<PagedResult<MovimentacaoEstoqueResponseDto>>> GetPagedAsync(
        int? produtoId,
        TipoMovimentacaoEstoque? tipo,
        int page,
        int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(produtoId, tipo, page, pageSize);
        return Result<PagedResult<MovimentacaoEstoqueResponseDto>>.Ok(new PagedResult<MovimentacaoEstoqueResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<List<ProdutoAlertaDto>>> GetAlertasAsync()
    {
        var produtos = await produtoRepository.GetAbaixoDoMinimoAsync();
        return Result<List<ProdutoAlertaDto>>.Ok(produtos.Select(p => new ProdutoAlertaDto
        {
            ProdutoId = p.Id,
            Nome = p.Nome,
            QuantidadeAtual = p.QuantidadeAtual,
            QuantidadeMinima = p.QuantidadeMinima
        }).ToList());
    }

    public async Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarEntradaAsync(RegistrarMovimentacaoEstoqueDto dto)
    {
        if (dto.Quantidade <= 0)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.InvalidValue, "A quantidade deve ser maior que zero.");

        var produto = await repository.GetProdutoAsync(dto.ProdutoId);
        if (produto is null)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");

        return await RegistrarAsync(produto, TipoMovimentacaoEstoque.Entrada, dto.Quantidade, dto.Observacao);
    }

    public async Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarSaidaAsync(RegistrarMovimentacaoEstoqueDto dto, TipoMovimentacaoEstoque tipo)
    {
        if (dto.Quantidade <= 0)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.InvalidValue, "A quantidade deve ser maior que zero.");

        var produto = await repository.GetProdutoAsync(dto.ProdutoId);
        if (produto is null)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");
        if (dto.Quantidade > produto.QuantidadeAtual)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.CannotModify, "Quantidade maior que o estoque disponível.");

        return await RegistrarAsync(produto, tipo, -dto.Quantidade, dto.Observacao);
    }

    public async Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarPerdaAsync(RegistrarMovimentacaoEstoqueDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Observacao))
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.EmptyField, "A observação da perda é obrigatória.");

        return await RegistrarSaidaAsync(dto, TipoMovimentacaoEstoque.Perda);
    }

    public async Task<Result<MovimentacaoEstoqueResponseDto>> AjustarAsync(AjustarEstoqueDto dto)
    {
        var observacao = dto.Observacao.Trim();
        if (string.IsNullOrWhiteSpace(observacao))
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.EmptyField, "A observação do ajuste é obrigatória.");
        if (dto.NovaQuantidade < 0)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.InvalidValue, "A nova quantidade não pode ser negativa.");

        var produto = await repository.GetProdutoAsync(dto.ProdutoId);
        if (produto is null)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");

        var anterior = produto.QuantidadeAtual;
        produto.QuantidadeAtual = dto.NovaQuantidade;
        produto.UpdatedByUserId = usuario.UserId;

        var movimentacao = new MovimentacaoEstoque
        {
            ClinicaId = usuario.ClinicaId,
            ProdutoId = produto.Id,
            Tipo = TipoMovimentacaoEstoque.Ajuste,
            Quantidade = Math.Abs(dto.NovaQuantidade - anterior),
            QuantidadeAnterior = anterior,
            QuantidadeAtual = produto.QuantidadeAtual,
            Observacao = observacao,
            UsuarioId = usuario.UserId
        };

        await repository.AddAsync(movimentacao);

        var depois = Map(movimentacao);
        await auditoria.RegistrarAsync("Estoque", "Ajustar", "MovimentacaoEstoque", movimentacao.Id, null, depois, observacao);

        return Result<MovimentacaoEstoqueResponseDto>.Ok(depois);
    }

    public async Task<Result<MovimentacaoEstoqueResponseDto>> CancelarAsync(int id, MotivoDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do cancelamento é obrigatório.");

        var movimentacao = await repository.GetByIdAsync(id);
        if (movimentacao is null)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.NotFound, "Movimentação não encontrada.");
        if (movimentacao.IsCancelada)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.CannotModify, "Movimentação já cancelada.");
        if (movimentacao.Tipo == TipoMovimentacaoEstoque.Compra)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.CannotModify,
                "Movimentações geradas por compra não podem ser canceladas diretamente.");

        var produto = await repository.GetProdutoAsync(movimentacao.ProdutoId);
        if (produto is null)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");

        var delta = movimentacao.Tipo switch
        {
            TipoMovimentacaoEstoque.Entrada => -movimentacao.Quantidade,
            TipoMovimentacaoEstoque.Saida or TipoMovimentacaoEstoque.Perda or TipoMovimentacaoEstoque.UsoInterno => movimentacao.Quantidade,
            TipoMovimentacaoEstoque.Ajuste when movimentacao.QuantidadeAtual > movimentacao.QuantidadeAnterior => -movimentacao.Quantidade,
            TipoMovimentacaoEstoque.Ajuste when movimentacao.QuantidadeAtual < movimentacao.QuantidadeAnterior => movimentacao.Quantidade,
            _ => 0
        };
        var novaQuantidade = produto.QuantidadeAtual + delta;
        if (novaQuantidade < 0)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.CannotModify,
                "Não é possível cancelar: o estoque já foi consumido por outras movimentações.");

        var antes = Map(movimentacao);

        produto.QuantidadeAtual = novaQuantidade;
        produto.UpdatedByUserId = usuario.UserId;
        movimentacao.IsCancelada = true;
        movimentacao.MotivoCancelamento = motivo;
        movimentacao.UpdatedAt = DateTime.UtcNow;
        await repository.SaveChangesAsync();

        var depois = Map(movimentacao);
        await auditoria.RegistrarAsync("Estoque", "Cancelar", "MovimentacaoEstoque", movimentacao.Id, antes, depois, motivo);

        return Result<MovimentacaoEstoqueResponseDto>.Ok(depois);
    }

    private async Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarAsync(
        Produto produto,
        TipoMovimentacaoEstoque tipo,
        int delta,
        string? observacao,
        string? origem = null,
        int? origemId = null)
    {
        var anterior = produto.QuantidadeAtual;
        produto.QuantidadeAtual += delta;
        produto.UpdatedByUserId = usuario.UserId;

        var movimentacao = new MovimentacaoEstoque
        {
            ClinicaId = usuario.ClinicaId,
            ProdutoId = produto.Id,
            Tipo = tipo,
            Quantidade = Math.Abs(delta),
            QuantidadeAnterior = anterior,
            QuantidadeAtual = produto.QuantidadeAtual,
            Origem = origem,
            OrigemId = origemId,
            Observacao = observacao,
            UsuarioId = usuario.UserId
        };

        await repository.AddAsync(movimentacao);

        var depois = Map(movimentacao);
        if (tipo == TipoMovimentacaoEstoque.Perda)
            await auditoria.RegistrarAsync("Estoque", "Perda", "MovimentacaoEstoque", movimentacao.Id, null, depois, observacao);

        return Result<MovimentacaoEstoqueResponseDto>.Ok(depois);
    }

    public async Task<Result<MovimentacaoEstoqueResponseDto>> RegistrarEntradaPorCompraAsync(int produtoId, int quantidade, int compraId)
    {
        if (quantidade <= 0)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.InvalidValue, "A quantidade deve ser maior que zero.");

        var produto = await repository.GetProdutoAsync(produtoId);
        if (produto is null)
            return Result<MovimentacaoEstoqueResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");

        return await RegistrarAsync(produto, TipoMovimentacaoEstoque.Compra, quantidade, null, "Compra", compraId);
    }
}
