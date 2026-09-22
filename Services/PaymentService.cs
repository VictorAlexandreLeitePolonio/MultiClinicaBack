using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Payment;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MultiClinica.API.Services;

public class PaymentService(
    IPaymentRepository repository,
    AppDbContext db,
    IUsuarioLogadoService usuario,
    TimeProvider timeProvider) : IPaymentService
{
    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<PaymentResponseDto>>> GetPagedAsync(
        int? patientId, PaymentStatus? status, DateOnly? referenceMonth,
        string? patientName, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(
            patientId, status, referenceMonth, patientName, page, pageSize);

        var data = items.Select(ToDto);

        return Result<PagedResult<PaymentResponseDto>>.Ok(new PagedResult<PaymentResponseDto>
        {
            Data       = data,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // ── Busca por Id ─────────────────────────────────────────────────────────

    public async Task<Result<PaymentResponseDto>> GetByIdAsync(int id)
    {
        var payment = await repository.GetByIdAsync(id);
        if (payment is null)
            return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Pagamento não encontrado.");

        return Result<PaymentResponseDto>.Ok(ToDto(payment));
    }

    private static Result<PaymentResponseDto>? ValidateReferenceMonth(DateOnly referenceMonth)
        => referenceMonth == default
            ? Result<PaymentResponseDto>.Fail(
                ErrorCodes.InvalidDate, "O mês de referência deve ser uma data válida.")
            : null;

    private async Task<DateOnly> TodayInClinicAsync()
    {
        var timeZoneId = await db.Clinicas
            .Where(clinic => clinic.Id == usuario.ClinicaId)
            .Select(clinic => clinic.TimeZoneId)
            .FirstOrDefaultAsync();

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? "UTC");
        }
        catch (TimeZoneNotFoundException)
        {
            timeZone = TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).DateTime);
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<PaymentResponseDto>> CreateAsync(CreatePaymentDto dto)
    {
        var referenceValidation = ValidateReferenceMonth(dto.ReferenceMonth);
        if (referenceValidation is not null)
            return referenceValidation;

        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.EmptyField, "O método de pagamento é obrigatório.");

        // Validações de existência
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == dto.ResponsavelId && u.ClinicaId == usuario.ClinicaId && !u.IsDeleted);
        var patient = await db.Patients.FirstOrDefaultAsync(p => p.Id == dto.PatientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == dto.PlanId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);

        if (user    is null) return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Usuário não encontrado.");
        if (patient is null) return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");
        if (plan    is null) return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");

        // Regras de negócio
        if (!patient.IsActive)
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.InactivePatient, "Não é possível registrar pagamento para um paciente inativo.");

        if (await repository.ExistsAsync(dto.PatientId, dto.ReferenceMonth))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.DuplicatePayment, "Já existe um pagamento para este paciente neste mês.");

        DateOnly? paidAt = dto.Status == PaymentStatus.Paid
            ? dto.PaidAt ?? await TodayInClinicAsync()
            : null;

        var payment = new Payment
        {
            ClinicaId      = usuario.ClinicaId,
            UserId         = dto.ResponsavelId,
            PatientId      = dto.PatientId,
            PlanId         = dto.PlanId,
            Amount         = plan.Valor,  // valor sempre vem do plano
            ReferenceMonth = dto.ReferenceMonth,
            PaymentMethod  = dto.PaymentMethod,
            Status         = dto.Status,
            PaidAt         = paidAt,
            PaymentDate    = dto.PaymentDate,
            CreatedByUserId = usuario.UserId,
        };

        try
        {
            await repository.AddAsync(payment);
        }
        catch (DbUpdateException exception) when (IsMonthlyDuplicate(exception))
        {
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.DuplicatePayment, "Já existe um pagamento para este paciente neste mês.");
        }

        // Monta DTO com os dados já em memória (evita nova query e referência circular)
        return Result<PaymentResponseDto>.Ok(new PaymentResponseDto
        {
            Id                  = payment.Id,
            ResponsavelId       = payment.UserId,
            PatientId           = payment.PatientId,
            PatientName         = patient.Name ?? string.Empty,
            PlanId              = payment.PlanId,
            PlanName            = plan.Name,
            PlanAmount          = plan.Valor,
            ReferenceMonth      = payment.ReferenceMonth,
            PaymentMethod       = payment.PaymentMethod,
            Status              = payment.Status,
            PaidAt              = payment.PaidAt,
            PaymentDate         = payment.PaymentDate,
            CreatedAt           = payment.CreatedAt
        });
    }

    // ── Atualização ──────────────────────────────────────────────────────────

    public async Task<Result<PaymentResponseDto>> UpdateAsync(int id, UpdatePaymentDto dto)
    {
        var referenceValidation = ValidateReferenceMonth(dto.ReferenceMonth);
        if (referenceValidation is not null)
            return referenceValidation;

        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.EmptyField, "O método de pagamento é obrigatório.");

        var payment = await repository.GetByIdAsync(id);
        if (payment is null)
            return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Pagamento não encontrado.");

        if (await repository.ExistsAsync(payment.PatientId, dto.ReferenceMonth, id))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.DuplicatePayment, "Já existe um pagamento para este paciente neste mês.");

        // Se o plano mudou, atualiza Amount
        if (dto.PlanId != payment.PlanId)
        {
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == dto.PlanId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
            if (plan is null)
                return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");
            payment.PlanId = dto.PlanId;
            payment.Amount = plan.Valor;
        }
        payment.ReferenceMonth = dto.ReferenceMonth;
        payment.PaymentMethod  = dto.PaymentMethod;
        payment.Status         = dto.Status;
        payment.PaymentDate    = dto.PaymentDate;
        payment.UpdatedByUserId = usuario.UserId;

        // Gerencia PaidAt automaticamente
        payment.PaidAt = dto.Status == PaymentStatus.Paid
            ? (dto.PaidAt ?? await TodayInClinicAsync())
            : null;
        try
        {
            await repository.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (IsMonthlyDuplicate(exception))
        {
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.DuplicatePayment, "Já existe um pagamento para este paciente neste mês.");
        }

        // Recarrega para retornar dados atualizados com navigations
        var updated = await repository.GetByIdAsync(id);
        return Result<PaymentResponseDto>.Ok(ToDto(updated!));
    }

    // ── Deleção ──────────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var payment = await repository.GetByIdAsync(id);
        if (payment is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Pagamento não encontrado.");

        if (payment.Status == PaymentStatus.Paid)
            return Result<bool>.Fail(
                ErrorCodes.CannotDelete,
                "Não é possível excluir um pagamento já confirmado. Cancele-o antes de excluir.");

        await repository.DeleteAsync(payment);
        return Result<bool>.Ok(true);
    }

    // ── Mapeamento ───────────────────────────────────────────────────────────

    private static PaymentResponseDto ToDto(Payment p) => new()
    {
        Id                  = p.Id,
        ResponsavelId       = p.UserId,
        PatientId           = p.PatientId,
        PatientName         = p.Patient.Name ?? string.Empty,
        PlanId              = p.PlanId,
        PlanName            = p.Plan.Name,
        PlanAmount          = p.Plan.Valor,
        ReferenceMonth      = p.ReferenceMonth,
        PaymentMethod       = p.PaymentMethod,
        Status              = p.Status,
        PaidAt              = p.PaidAt,
        PaymentDate         = p.PaymentDate,
        CreatedAt           = p.CreatedAt
    };

    private static bool IsMonthlyDuplicate(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
            && postgres.ConstraintName == "IX_Payments_ClinicaId_PatientId_ReferenceMonth_Month";
}
