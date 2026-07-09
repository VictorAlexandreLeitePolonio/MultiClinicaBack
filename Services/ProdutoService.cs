using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ProdutoService(
    IProdutoRepository repository,
    IAuditoriaFinanceiraService auditoria,
    IUsuarioLogadoService usuario) : IProdutoService
{
    private ProdutoResponseDto Map(Produto p) => new()
    {
        Id = p.Id,
        CategoriaProdutoId = p.CategoriaProdutoId,
        Nome = p.Nome,
        Descricao = p.Descricao,
        CodigoInterno = p.CodigoInterno,
        CodigoBarras = p.CodigoBarras,
        ValorCompra = PermissionMatrix.Has(usuario.Role, Permissions.Produtos.VisualizarCusto) ? p.ValorCompra : null,
        ValorVenda = PermissionMatrix.Has(usuario.Role, Permissions.Produtos.VisualizarPrecoVenda) ? p.ValorVenda : null,
        QuantidadeAtual = p.QuantidadeAtual,
        QuantidadeMinima = p.QuantidadeMinima,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt
    };

    public async Task<Result<PagedResult<ProdutoResponseDto>>> GetPagedAsync(
        string? nome, int? categoriaProdutoId, bool? ativo, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(nome, categoriaProdutoId, ativo, page, pageSize);
        return Result<PagedResult<ProdutoResponseDto>>.Ok(new PagedResult<ProdutoResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<ProdutoResponseDto>> GetByIdAsync(int id)
    {
        var produto = await repository.GetByIdAsync(id);
        return produto is null
            ? Result<ProdutoResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.")
            : Result<ProdutoResponseDto>.Ok(Map(produto));
    }

    public async Task<Result<ProdutoResponseDto>> CreateAsync(CreateProdutoDto dto)
    {
        var validation = await ValidateAsync(dto.CategoriaProdutoId, dto.Nome, dto.ValorCompra, dto.ValorVenda);
        if (validation is not null)
            return validation;

        var entity = new Produto
        {
            ClinicaId = usuario.ClinicaId,
            CategoriaProdutoId = dto.CategoriaProdutoId,
            Nome = dto.Nome.Trim(),
            Descricao = dto.Descricao,
            CodigoInterno = dto.CodigoInterno,
            CodigoBarras = dto.CodigoBarras,
            ValorCompra = dto.ValorCompra,
            ValorVenda = dto.ValorVenda,
            QuantidadeMinima = dto.QuantidadeMinima,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(entity);

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Produtos", "Criar", "Produto", entity.Id, null, depois);

        return Result<ProdutoResponseDto>.Ok(depois);
    }

    public async Task<Result<ProdutoResponseDto>> UpdateAsync(int id, UpdateProdutoDto dto)
    {
        var validation = await ValidateAsync(dto.CategoriaProdutoId, dto.Nome, dto.ValorCompra, dto.ValorVenda);
        if (validation is not null)
            return validation;

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ProdutoResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");

        var antes = Map(entity);

        entity.CategoriaProdutoId = dto.CategoriaProdutoId;
        entity.Nome = dto.Nome.Trim();
        entity.Descricao = dto.Descricao;
        entity.CodigoInterno = dto.CodigoInterno;
        entity.CodigoBarras = dto.CodigoBarras;
        entity.ValorCompra = dto.ValorCompra;
        entity.ValorVenda = dto.ValorVenda;
        entity.QuantidadeMinima = dto.QuantidadeMinima;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        var depois = Map(entity);
        await auditoria.RegistrarAsync("Produtos", "Editar", "Produto", entity.Id, antes, depois);

        return Result<ProdutoResponseDto>.Ok(depois);
    }

    public async Task<Result<ProdutoResponseDto>> SetActiveAsync(int id, bool active)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ProdutoResponseDto>.Fail(ErrorCodes.NotFound, "Produto não encontrado.");

        entity.IsActive = active;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();
        return Result<ProdutoResponseDto>.Ok(Map(entity));
    }

    private async Task<Result<ProdutoResponseDto>?> ValidateAsync(
        int? categoriaProdutoId, string nome, decimal valorCompra, decimal valorVenda)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result<ProdutoResponseDto>.Fail(ErrorCodes.EmptyField, "O nome é obrigatório.");
        if (valorCompra < 0)
            return Result<ProdutoResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor de compra não pode ser negativo.");
        if (valorVenda < 0)
            return Result<ProdutoResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor de venda não pode ser negativo.");
        if (categoriaProdutoId is not null && !await repository.CategoriaExistsAsync(categoriaProdutoId.Value))
            return Result<ProdutoResponseDto>.Fail(ErrorCodes.NotFound, "Categoria de produto não encontrada.");

        return null;
    }
}
