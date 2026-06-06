using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class EvolutionTemplateService(AppDbContext db, IUsuarioLogadoService usuario) : IEvolutionTemplateService
{
    public async Task<Result<PagedResult<EvolutionTemplateResponseDto>>> GetPagedAsync(int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.EvolutionTemplates
            .Where(t => t.ClinicaId == usuario.ClinicaId && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .AsQueryable();

        var total = await query.CountAsync();
        var data = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => ToResponse(t))
            .ToListAsync();

        return Result<PagedResult<EvolutionTemplateResponseDto>>.Ok(new PagedResult<EvolutionTemplateResponseDto>
        {
            Data = data,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    public async Task<Result<EvolutionTemplateResponseDto>> GetByIdAsync(int id)
    {
        var template = await db.EvolutionTemplates.FirstOrDefaultAsync(t =>
            t.Id == id && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted);

        return template is null
            ? Result<EvolutionTemplateResponseDto>.Fail(ErrorCodes.NotFound, "Modelo de evolução não encontrado.")
            : Result<EvolutionTemplateResponseDto>.Ok(ToResponse(template));
    }

    public async Task<Result<EvolutionTemplateResponseDto>> CreateAsync(CreateEvolutionTemplateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<EvolutionTemplateResponseDto>.Fail(ErrorCodes.EmptyField, "Nome do modelo é obrigatório.");

        var template = new EvolutionTemplate
        {
            ClinicaId = usuario.ClinicaId,
            Name = dto.Name.Trim(),
            Description = NormalizeOptional(dto.Description),
            Category = NormalizeOptional(dto.Category),
            IsDefault = dto.IsDefault,
            CreatedByUserId = usuario.UserId
        };

        db.EvolutionTemplates.Add(template);
        await db.SaveChangesAsync();

        return Result<EvolutionTemplateResponseDto>.Ok(ToResponse(template));
    }

    public async Task<Result<bool>> UpdateAsync(int id, UpdateEvolutionTemplateDto dto)
    {
        var template = await db.EvolutionTemplates.FirstOrDefaultAsync(t =>
            t.Id == id && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted);

        if (template is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Modelo de evolução não encontrado.");

        if (dto.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Result<bool>.Fail(ErrorCodes.EmptyField, "Nome do modelo é obrigatório.");
            template.Name = dto.Name.Trim();
        }

        if (dto.Description is not null)
            template.Description = NormalizeOptional(dto.Description);
        if (dto.Category is not null)
            template.Category = NormalizeOptional(dto.Category);
        if (dto.IsDefault.HasValue)
            template.IsDefault = dto.IsDefault.Value;
        if (dto.IsActive.HasValue)
            template.IsActive = dto.IsActive.Value;

        template.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> DeactivateAsync(int id)
    {
        var template = await db.EvolutionTemplates.FirstOrDefaultAsync(t =>
            t.Id == id && t.ClinicaId == usuario.ClinicaId && !t.IsDeleted);

        if (template is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Modelo de evolução não encontrado.");

        template.IsActive = false;
        template.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();

        return Result<bool>.Ok(true);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EvolutionTemplateResponseDto ToResponse(EvolutionTemplate template)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            IsActive = template.IsActive,
            IsDefault = template.IsDefault,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
}
