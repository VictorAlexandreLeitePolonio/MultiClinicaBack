using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Plans;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PlanService(IPlanRepository repository, SessionTypeRepository sessionTypes, IUsuarioLogadoService usuario) : IPlanService
{
    private static PlanResponseDto Map(Plans plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        Valor = plan.Valor,
        TipoPlano = plan.TipoPlano,
        TipoSessaoId = plan.TipoSessaoId,
        TipoSessaoName = plan.TipoSessao.Name,
        IsActive = plan.IsActive,
        CreatedAt = plan.CreatedAt
    };

    public async Task<Result<PagedResult<PlanResponseDto>>> GetPagedAsync(
        TipoPlano? tipoPlano, int? tipoSessaoId, bool? isActive, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(tipoPlano, tipoSessaoId, isActive, page, pageSize);
        return Result<PagedResult<PlanResponseDto>>.Ok(new PagedResult<PlanResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<PlanResponseDto>> GetByIdAsync(int id)
    {
        var plan = await repository.GetByIdAsync(id);
        return plan is null
            ? Result<PlanResponseDto>.Fail(ErrorCodes.NotFound, "Plano não encontrado.")
            : Result<PlanResponseDto>.Ok(Map(plan));
    }

    public async Task<Result<PlanResponseDto>> CreateAsync(CreatePlanDto dto)
    {
        if (dto.Valor <= 0)
            return Result<PlanResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor do plano deve ser maior que zero.");
        if (await repository.NameExistsAsync(dto.Name))
            return Result<PlanResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe um plano com este nome.");

        var sessionType = await sessionTypes.GetByIdAsync(dto.TipoSessaoId);
        if (sessionType is null)
            return Result<PlanResponseDto>.Fail(ErrorCodes.InvalidValue, "Tipo de sessão inválido.");

        var plan = new Plans
        {
            ClinicaId = usuario.ClinicaId,
            Name = dto.Name,
            Valor = dto.Valor,
            TipoPlano = dto.TipoPlano,
            TipoSessaoId = sessionType.Id,
            TipoSessao = sessionType,
            CreatedByUserId = usuario.UserId
        };

        await repository.AddAsync(plan);
        return Result<PlanResponseDto>.Ok(Map(plan));
    }

    public async Task<Result<PlanResponseDto>> UpdateAsync(int id, UpdatePlanDto dto)
    {
        if (dto.Valor <= 0)
            return Result<PlanResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor do plano deve ser maior que zero.");

        var plan = await repository.GetByIdAsync(id);
        if (plan is null)
            return Result<PlanResponseDto>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");
        if (await repository.NameExistsAsync(dto.Name, id))
            return Result<PlanResponseDto>.Fail(ErrorCodes.DuplicateName, "Já existe um plano com este nome.");

        var sessionType = await sessionTypes.GetByIdAsync(dto.TipoSessaoId);
        if (sessionType is null)
            return Result<PlanResponseDto>.Fail(ErrorCodes.InvalidValue, "Tipo de sessão inválido.");

        plan.Name = dto.Name;
        plan.Valor = dto.Valor;
        plan.TipoPlano = dto.TipoPlano;
        plan.TipoSessaoId = sessionType.Id;
        plan.TipoSessao = sessionType;
        plan.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();
        return Result<PlanResponseDto>.Ok(Map(plan));
    }

    public async Task<Result<bool>> ToggleStatusAsync(int id)
    {
        var plan = await repository.GetByIdAsync(id);
        if (plan is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");

        plan.IsActive = !plan.IsActive;
        await repository.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var plan = await repository.GetByIdAsync(id);
        if (plan is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");
        if (await repository.HasPaymentsAsync(id))
            return Result<bool>.Fail(ErrorCodes.HasAssociatedRecords, "Não é possível excluir um plano com pagamentos associados.");

        await repository.DeleteAsync(plan);
        return Result<bool>.Ok(true);
    }
}
