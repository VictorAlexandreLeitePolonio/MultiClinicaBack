using System.Text.RegularExpressions;
using ProjetoLP.API.Common;
using ProjetoLP.API.Data;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Payment;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;
using ProjetoLP.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ProjetoLP.API.Services;

public partial class PaymentService(IPaymentRepository repository, AppDbContext db) : IPaymentService
{
    [GeneratedRegex(@"^\d{4}-\d{2}$")]
    private static partial Regex ReferenceMonthRegex();

    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<PaymentResponseDto>>> GetPagedAsync(
        int? patientId, PaymentStatus? status, string? referenceMonth,
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

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<PaymentResponseDto>> CreateAsync(CreatePaymentDto dto)
    {
        // Validações de formato
        if (!ReferenceMonthRegex().IsMatch(dto.ReferenceMonth))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.InvalidFormat, "O formato do mês de referência deve ser 'YYYY-MM'.");

        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.EmptyField, "O método de pagamento é obrigatório.");

        // Validações de existência
        var user    = await db.Users.FindAsync(dto.UserId);
        var patient = await db.Patients.FindAsync(dto.PatientId);
        var plan    = await db.Plans.FindAsync(dto.PlanId);

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

        var payment = new Payment
        {
            UserId         = dto.UserId,
            PatientId      = dto.PatientId,
            PlanId         = dto.PlanId,
            Amount         = plan.Valor,  // valor sempre vem do plano
            ReferenceMonth = dto.ReferenceMonth,
            PaymentMethod  = dto.PaymentMethod,
            PaymentDate    = dto.PaymentDate,
        };

        await repository.AddAsync(payment);

        // Monta DTO com os dados já em memória (evita nova query e referência circular)
        return Result<PaymentResponseDto>.Ok(new PaymentResponseDto
        {
            Id                  = payment.Id,
            UserId              = payment.UserId,
            PatientId           = payment.PatientId,
            PatientName         = patient.Name,
            PlanId              = payment.PlanId,
            PlanName            = plan.Name,
            PlanAmount          = plan.Valor,
            ReferenceMonth      = payment.ReferenceMonth,
            PaymentMethod       = payment.PaymentMethod,
            Status              = payment.Status,
            PaidAt              = payment.PaidAt,
            PaymentDate         = payment.PaymentDate,
            PaymentReminderSent = payment.PaymentReminderSent,
            CreatedAt           = payment.CreatedAt
        });
    }

    // ── Atualização ──────────────────────────────────────────────────────────

    public async Task<Result<PaymentResponseDto>> UpdateAsync(int id, UpdatePaymentDto dto)
    {
        if (!ReferenceMonthRegex().IsMatch(dto.ReferenceMonth))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.InvalidFormat, "O formato do mês de referência deve ser 'YYYY-MM'.");

        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
            return Result<PaymentResponseDto>.Fail(
                ErrorCodes.EmptyField, "O método de pagamento é obrigatório.");

        var payment = await repository.GetByIdAsync(id);
        if (payment is null)
            return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Pagamento não encontrado.");

        // Se o plano mudou, atualiza Amount
        if (dto.PlanId != payment.PlanId)
        {
            var plan = await db.Plans.FindAsync(dto.PlanId);
            if (plan is null)
                return Result<PaymentResponseDto>.Fail(ErrorCodes.NotFound, "Plano não encontrado.");
            payment.PlanId = dto.PlanId;
            payment.Amount = plan.Valor;
        }

        var oldPaymentDate = payment.PaymentDate;

        payment.ReferenceMonth = dto.ReferenceMonth;
        payment.PaymentMethod  = dto.PaymentMethod;
        payment.Status         = dto.Status;
        payment.PaymentDate    = dto.PaymentDate;

        // Gerencia PaidAt automaticamente
        payment.PaidAt = dto.Status == PaymentStatus.Paid
            ? (dto.PaidAt ?? DateTime.UtcNow)
            : null;

        // Se data de vencimento mudou, reseta flag de lembrete
        if (dto.PaymentDate != oldPaymentDate)
            payment.PaymentReminderSent = false;

        await repository.SaveChangesAsync();

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
        UserId              = p.UserId,
        PatientId           = p.PatientId,
        PatientName         = p.Patient.Name,
        PlanId              = p.PlanId,
        PlanName            = p.Plan.Name,
        PlanAmount          = p.Plan.Valor,
        ReferenceMonth      = p.ReferenceMonth,
        PaymentMethod       = p.PaymentMethod,
        Status              = p.Status,
        PaidAt              = p.PaidAt,
        PaymentDate         = p.PaymentDate,
        PaymentReminderSent = p.PaymentReminderSent,
        CreatedAt           = p.CreatedAt
    };
}
