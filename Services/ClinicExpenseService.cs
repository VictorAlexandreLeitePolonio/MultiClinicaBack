using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ClinicExpenseService(
    IClinicExpenseRepository repository,
    IUsuarioLogadoService usuario) : IClinicExpenseService
{
    private static ClinicExpenseResponseDto Map(ClinicExpense e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Amount = e.Amount,
        Date = e.Date,
        Description = e.Description,
        CreatedAt = e.CreatedAt
    };

    public async Task<Result<PagedResult<ClinicExpenseResponseDto>>> GetPagedAsync(
        DateTime? startDate, DateTime? endDate, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(startDate, endDate, page, pageSize);
        return Result<PagedResult<ClinicExpenseResponseDto>>.Ok(new PagedResult<ClinicExpenseResponseDto>
        {
            Data = items.Select(Map),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<ClinicExpenseResponseDto>> GetByIdAsync(int id)
    {
        var expense = await repository.GetByIdAsync(id);
        return expense is null
            ? Result<ClinicExpenseResponseDto>.Fail(ErrorCodes.NotFound, "Gasto não encontrado.")
            : Result<ClinicExpenseResponseDto>.Ok(Map(expense));
    }

    public async Task<Result<ClinicExpenseResponseDto>> CreateAsync(CreateClinicExpenseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ClinicExpenseResponseDto>.Fail(ErrorCodes.EmptyField, "O título do gasto é obrigatório.");
        if (dto.Amount <= 0)
            return Result<ClinicExpenseResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor do gasto deve ser maior que zero.");

        var entity = new ClinicExpense
        {
            ClinicaId = usuario.ClinicaId,
            Title = dto.Title.Trim(),
            Amount = dto.Amount,
            Date = dto.Date,
            Description = dto.Description,
            CreatedByUserId = usuario.UserId
        };
        await repository.AddAsync(entity);

        return Result<ClinicExpenseResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<ClinicExpenseResponseDto>> UpdateAsync(int id, UpdateClinicExpenseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ClinicExpenseResponseDto>.Fail(ErrorCodes.EmptyField, "O título do gasto é obrigatório.");
        if (dto.Amount <= 0)
            return Result<ClinicExpenseResponseDto>.Fail(ErrorCodes.InvalidValue, "O valor do gasto deve ser maior que zero.");

        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<ClinicExpenseResponseDto>.Fail(ErrorCodes.NotFound, "Gasto não encontrado.");

        entity.Title = dto.Title.Trim();
        entity.Amount = dto.Amount;
        entity.Date = dto.Date;
        entity.Description = dto.Description;
        entity.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        return Result<ClinicExpenseResponseDto>.Ok(Map(entity));
    }

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Gasto não encontrado.");

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        return Result<bool>.Ok(true);
    }
}
