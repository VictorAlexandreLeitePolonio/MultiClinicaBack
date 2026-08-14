using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class CompraService(
    CompraRepository repository,
    ProdutoRepository produtoRepository,
    MovimentacaoEstoqueService estoqueService,
    IAuditoriaFinanceiraService auditoria,
    IUsuarioLogadoService usuario)
{
    private CompraResponseDto Map(Compra c)
    {
        var podeVerValores = PermissionMatrix.Has(usuario.Role, Permissions.Compras.VisualizarValores);
        return new CompraResponseDto
        {
            Id = c.Id,
            FornecedorId = c.FornecedorId,
            DataCompra = c.DataCompra,
            ValorTotal = podeVerValores ? c.ValorTotal : null,
            Status = c.Status,
            Observacao = c.Observacao,
            CreatedAt = c.CreatedAt,
            Itens = c.Itens.Select(i => new CompraItemResponseDto
            {
                Id = i.Id,
                ProdutoId = i.ProdutoId,
                Quantidade = i.Quantidade,
                ValorUnitario = podeVerValores ? i.ValorUnitario : null,
                ValorTotal = podeVerValores ? i.ValorTotal : null
            }).ToList()
        };
    }

    public async Task<Result<PagedResult<CompraResponseDto>>> GetPagedAsync(int? fornecedorId, StatusCompra? status, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(fornecedorId, status, page, pageSize);
        return Result<PagedResult<CompraResponseDto>>.Ok(new PagedResult<CompraResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<CompraResponseDto>> GetByIdAsync(int id)
    {
        var compra = await repository.GetByIdAsync(id);
        return compra is null
            ? Result<CompraResponseDto>.Fail(ErrorCodes.NotFound, "Compra não encontrada.")
            : Result<CompraResponseDto>.Ok(Map(compra));
    }

    public async Task<Result<CompraResponseDto>> CreateAsync(CreateCompraDto dto)
    {
        var validation = await ValidateAsync(dto.FornecedorId, dto.Itens);
        if (validation is not null)
            return validation;

        var entity = new Compra
        {
            ClinicaId = usuario.ClinicaId,
            FornecedorId = dto.FornecedorId,
            DataCompra = DateTime.SpecifyKind(dto.DataCompra, DateTimeKind.Utc),
            Observacao = dto.Observacao,
            CreatedByUserId = usuario.UserId
        };
        ReplaceItems(entity, dto.Itens);

        await repository.AddAsync(entity);

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Compras", "Criar", "Compra", entity.Id, null, depois);

        return Result<CompraResponseDto>.Ok(depois);
    }

    public async Task<Result<CompraResponseDto>> UpdateAsync(int id, UpdateCompraDto dto)
    {
        var validation = await ValidateAsync(dto.FornecedorId, dto.Itens);
        if (validation is not null)
            return validation;

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CompraResponseDto>.Fail(ErrorCodes.NotFound, "Compra não encontrada.");
        if (entity.Status != StatusCompra.Aberta)
            return Result<CompraResponseDto>.Fail(ErrorCodes.CannotModify, "Só é possível editar uma compra em aberto.");

        entity.FornecedorId = dto.FornecedorId;
        entity.DataCompra = DateTime.SpecifyKind(dto.DataCompra, DateTimeKind.Utc);
        entity.Observacao = dto.Observacao;
        entity.Itens.Clear();
        ReplaceItems(entity, dto.Itens);
        entity.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();
        return Result<CompraResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<CompraResponseDto>> AprovarAsync(int id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CompraResponseDto>.Fail(ErrorCodes.NotFound, "Compra não encontrada.");
        if (entity.Status != StatusCompra.Aberta)
            return Result<CompraResponseDto>.Fail(ErrorCodes.CannotModify, "Só é possível aprovar uma compra em aberto.");

        var antes = Map(entity);
        entity.Status = StatusCompra.Aprovada;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Compras", "Aprovar", "Compra", entity.Id, antes, depois);

        return Result<CompraResponseDto>.Ok(depois);
    }

    public async Task<Result<CompraResponseDto>> ReceberAsync(int id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CompraResponseDto>.Fail(ErrorCodes.NotFound, "Compra não encontrada.");
        if (entity.Status != StatusCompra.Aprovada)
            return Result<CompraResponseDto>.Fail(ErrorCodes.CannotModify, "Só é possível receber uma compra aprovada.");

        foreach (var item in entity.Itens)
        {
            var resultado = await estoqueService.RegistrarEntradaPorCompraAsync(item.ProdutoId, item.Quantidade, entity.Id);
            if (!resultado.IsSuccess)
                return Result<CompraResponseDto>.Fail(resultado.ErrorCode!, resultado.ErrorMessage!);
        }

        var antes = Map(entity);
        entity.Status = StatusCompra.Recebida;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Compras", "Receber", "Compra", entity.Id, antes, depois);

        return Result<CompraResponseDto>.Ok(depois);
    }

    public async Task<Result<CompraResponseDto>> CancelarAsync(int id, MotivoDto dto)
    {
        var motivo = dto.Motivo.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return Result<CompraResponseDto>.Fail(ErrorCodes.EmptyField, "O motivo do cancelamento é obrigatório.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<CompraResponseDto>.Fail(ErrorCodes.NotFound, "Compra não encontrada.");
        if (entity.Status is StatusCompra.Recebida or StatusCompra.Cancelada)
            return Result<CompraResponseDto>.Fail(ErrorCodes.CannotModify, "Não é possível cancelar uma compra recebida ou já cancelada.");

        var antes = Map(entity);
        entity.Status = StatusCompra.Cancelada;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Compras", "Cancelar", "Compra", entity.Id, antes, depois, motivo);

        return Result<CompraResponseDto>.Ok(depois);
    }

    private void ReplaceItems(Compra entity, List<CompraItemDto> itens)
    {
        foreach (var item in itens)
        {
            entity.Itens.Add(new CompraItem
            {
                ClinicaId = usuario.ClinicaId,
                ProdutoId = item.ProdutoId,
                Quantidade = item.Quantidade,
                ValorUnitario = item.ValorUnitario,
                ValorTotal = item.Quantidade * item.ValorUnitario,
                CreatedByUserId = usuario.UserId
            });
        }

        entity.ValorTotal = entity.Itens.Sum(i => i.ValorTotal);
    }

    private async Task<Result<CompraResponseDto>?> ValidateAsync(int fornecedorId, List<CompraItemDto> itens)
    {
        if (!await repository.FornecedorExistsAsync(fornecedorId))
            return Result<CompraResponseDto>.Fail(ErrorCodes.NotFound, "Fornecedor não encontrado.");
        if (itens.Count == 0)
            return Result<CompraResponseDto>.Fail(ErrorCodes.EmptyField, "A compra precisa de ao menos um item.");

        foreach (var item in itens)
        {
            if (item.Quantidade <= 0)
                return Result<CompraResponseDto>.Fail(ErrorCodes.InvalidValue, "A quantidade do item deve ser maior que zero.");
            if (item.ValorUnitario <= 0)
                return Result<CompraResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor unitário do item deve ser maior que zero.");
            if (await produtoRepository.GetByIdAsync(item.ProdutoId) is null)
                return Result<CompraResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");
        }

        return null;
    }
}
