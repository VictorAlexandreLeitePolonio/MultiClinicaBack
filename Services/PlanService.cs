using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Plans;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Services;

public class PlanService(IPlanRepository repository) : IPlanService
{
    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<PlanResponseDto>>> GetPagedAsync(
        TipoPlano? tipoPlano, TipoSessao? tipoSessao, bool? isActive, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(
            tipoPlano, tipoSessao, isActive, page, pageSize);

        var data = items.Select(p => new PlanResponseDto
        {
            Id         = p.Id,
            Name       = p.Name,
            Valor      = p.Valor,
            TipoPlano  = p.TipoPlano,
            TipoSessao = p.TipoSessao,
            IsActive   = p.IsActive,
            CreatedAt  = p.CreatedAt,
        });

        return Result<PagedResult<PlanResponseDto>>.Ok(new PagedResult<PlanResponseDto>
        {
            Data       = data,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // ── Busca por Id ─────────────────────────────────────────────────────────

    public async Task<Result<PlanResponseDto>> GetByIdAsync(int id)
    {
        var plan = await repository.GetByIdAsync(id);
        if (plan is null)
            return Result<PlanResponseDto>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");

        return Result<PlanResponseDto>.Ok(new PlanResponseDto
        {
            Id         = plan.Id,
            Name       = plan.Name,
            Valor      = plan.Valor,
            TipoPlano  = plan.TipoPlano,
            TipoSessao = plan.TipoSessao,
            IsActive   = plan.IsActive,
            CreatedAt  = plan.CreatedAt,
        });
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<PlanResponseDto>> CreateAsync(CreatePlanDto dto)
    {
        if (dto.Valor <= 0)
            return Result<PlanResponseDto>.Fail(
                ErrorCodes.InvalidValue, "O valor do plano deve ser maior que zero.");

        if (await repository.NameExistsAsync(dto.Name))
            return Result<PlanResponseDto>.Fail(
                ErrorCodes.DuplicateName, "Já existe um plano com este nome.");

        var plan = new Plans
        {
            Name       = dto.Name,
            Valor      = dto.Valor,
            TipoPlano  = dto.TipoPlano,
            TipoSessao = dto.TipoSessao,
        };

        await repository.AddAsync(plan);

        return Result<PlanResponseDto>.Ok(new PlanResponseDto
        {
            Id         = plan.Id,
            Name       = plan.Name,
            Valor      = plan.Valor,
            TipoPlano  = plan.TipoPlano,
            TipoSessao = plan.TipoSessao,
            IsActive   = plan.IsActive,
            CreatedAt  = plan.CreatedAt,
        });
    }

    // ── Atualização ──────────────────────────────────────────────────────────

    public async Task<Result<PlanResponseDto>> UpdateAsync(int id, UpdatePlanDto dto)
    {
        if (dto.Valor <= 0)
            return Result<PlanResponseDto>.Fail(
                ErrorCodes.InvalidValue, "O valor do plano deve ser maior que zero.");

        var plan = await repository.GetByIdAsync(id);
        if (plan is null)
            return Result<PlanResponseDto>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");

        if (await repository.NameExistsAsync(dto.Name, id))
            return Result<PlanResponseDto>.Fail(
                ErrorCodes.DuplicateName, "Já existe um plano com este nome.");

        plan.Name       = dto.Name;
        plan.Valor      = dto.Valor;
        plan.TipoPlano  = dto.TipoPlano;
        plan.TipoSessao = dto.TipoSessao;

        await repository.SaveChangesAsync();

        return Result<PlanResponseDto>.Ok(new PlanResponseDto
        {
            Id         = plan.Id,
            Name       = plan.Name,
            Valor      = plan.Valor,
            TipoPlano  = plan.TipoPlano,
            TipoSessao = plan.TipoSessao,
            IsActive   = plan.IsActive,
            CreatedAt  = plan.CreatedAt,
        });
    }

    // ── Toggle Status ────────────────────────────────────────────────────────

    public async Task<Result<bool>> ToggleStatusAsync(int id)
    {
        var plan = await repository.GetByIdAsync(id);
        if (plan is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");

        plan.IsActive = !plan.IsActive;
        await repository.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    // ── Deleção ──────────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var plan = await repository.GetByIdAsync(id);
        if (plan is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");

        if (await repository.HasPaymentsAsync(id))
            return Result<bool>.Fail(
                ErrorCodes.HasAssociatedRecords, "Não é possível excluir um plano com pagamentos associados.");

        await repository.DeleteAsync(plan);
        return Result<bool>.Ok(true);
    }
}
