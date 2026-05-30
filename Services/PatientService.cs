using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Patient;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PatientService(IPatientRepository repository, IUsuarioLogadoService usuario) : IPatientService
{
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? DigitsOnly(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsDigit).ToArray());

    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<PatientResponseDto>>> GetPagedAsync(
        string? name, bool? isActive, AppointmentStatus? appointmentStatus,
        PaymentStatus? paymentStatus, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(
            name, isActive, appointmentStatus, paymentStatus, page, pageSize);

        var data = items.Select(p => new PatientResponseDto
        {
            Id                = p.Id,
            Name              = p.Name,
            Email             = p.Email,
            CPF               = p.CPF,
            Rg                = p.Rg,
            Rua               = p.Rua,
            Numero            = p.Numero,
            Bairro            = p.Bairro,
            Cidade            = p.Cidade,
            Estado            = p.Estado,
            Cep               = p.Cep,
            Phone             = p.Phone,
            IsActive          = p.IsActive,
            appointmentStatus = p.Appointments.OrderByDescending(a => a.AppointmentDate).FirstOrDefault()?.Status ?? AppointmentStatus.Scheduled,
            paymentStatus     = p.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault()?.Status ?? PaymentStatus.Pending,
            CreatedAt         = p.CreatedAt,
        });

        return Result<PagedResult<PatientResponseDto>>.Ok(new PagedResult<PatientResponseDto>
        {
            Data       = data,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // ── Busca por Id ─────────────────────────────────────────────────────────

    public async Task<Result<PatientResponseDto>> GetByIdAsync(int id)
    {
        var patient = await repository.GetByIdAsync(id);
        if (patient is null)
            return Result<PatientResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        return Result<PatientResponseDto>.Ok(new PatientResponseDto
        {
            Id                = patient.Id,
            Name              = patient.Name,
            Email             = patient.Email,
            CPF               = patient.CPF,
            Rg                = patient.Rg,
            Rua               = patient.Rua,
            Numero            = patient.Numero,
            Bairro            = patient.Bairro,
            Cidade            = patient.Cidade,
            Estado            = patient.Estado,
            Cep               = patient.Cep,
            Phone             = patient.Phone,
            IsActive          = patient.IsActive,
            appointmentStatus = patient.Appointments.OrderByDescending(a => a.AppointmentDate).FirstOrDefault()?.Status ?? AppointmentStatus.Scheduled,
            paymentStatus     = patient.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault()?.Status ?? PaymentStatus.Pending,
            CreatedAt         = patient.CreatedAt,
        });
    }

    // ── Perfil Completo ──────────────────────────────────────────────────────

    public async Task<Result<PatientProfileDto>> GetProfileAsync(int id)
    {
        var patient = await repository.GetByIdWithDetailsAsync(id);
        if (patient is null)
            return Result<PatientProfileDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        var profile = new PatientProfileDto
        {
            Id        = patient.Id,
            Name      = patient.Name,
            Email     = patient.Email,
            CPF       = patient.CPF,
            Rg        = patient.Rg,
            Phone     = patient.Phone,
            Rua       = patient.Rua,
            Numero    = patient.Numero,
            Bairro    = patient.Bairro,
            Cidade    = patient.Cidade,
            Estado    = patient.Estado,
            Cep       = patient.Cep,
            IsActive  = patient.IsActive,
            CreatedAt = patient.CreatedAt,

            Appointments = patient.Appointments
                .OrderByDescending(a => a.AppointmentDate)
                .Select(a => new AppointmentSummary
                {
                    Id              = a.Id,
                    AppointmentDate = a.AppointmentDate,
                    Status          = a.Status,
                    UserName        = a.User.Name,
                    CreatedAt       = a.CreatedAt,
                }).ToList(),

            MedicalRecords = patient.MedicalRecords
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MedicalRecordSummary
                {
                    Id        = m.Id,
                    Titulo    = m.Titulo,
                    Sessao    = m.Sessao,
                    Patologia = m.Patologia,
                    UserName  = m.User.Name,
                    CreatedAt = m.CreatedAt,
                }).ToList(),

            Payments = patient.Payments
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentSummary
                {
                    Id                  = p.Id,
                    ReferenceMonth      = p.ReferenceMonth,
                    PlanName            = p.Plan.Name,
                    Amount              = p.Amount,
                    PaymentMethod       = p.PaymentMethod,
                    Status              = p.Status,
                    PaymentDate         = p.PaymentDate,
                    PaidAt              = p.PaidAt,
                    CreatedAt           = p.CreatedAt,
                }).ToList(),
        };

        return Result<PatientProfileDto>.Ok(profile);
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<PatientResponseDto>> CreateAsync(CreatePatientDto dto)
    {
        var normalizedEmail = NormalizeOptional(dto.Email);
        var normalizedCpf = DigitsOnly(dto.CPF);

        // Validações de unicidade
        if (await repository.EmailExistsAsync(normalizedEmail))
            return Result<PatientResponseDto>.Fail(ErrorCodes.DuplicateEmail, "Email já cadastrado por outro paciente.");

        if (await repository.CpfExistsAsync(normalizedCpf))
            return Result<PatientResponseDto>.Fail(ErrorCodes.DuplicateCpf, "CPF já cadastrado por outro paciente.");

        var patient = new Patient
        {
            ClinicaId = usuario.ClinicaId,
            Name   = NormalizeOptional(dto.Name),
            Email  = normalizedEmail,
            CPF    = normalizedCpf,
            Rg     = NormalizeOptional(dto.Rg),
            Rua    = NormalizeOptional(dto.Rua),
            Numero = NormalizeOptional(dto.Numero),
            Bairro = NormalizeOptional(dto.Bairro),
            Cidade = NormalizeOptional(dto.Cidade),
            Estado = NormalizeOptional(dto.Estado),
            Cep    = DigitsOnly(dto.Cep),
            Phone  = DigitsOnly(dto.Phone),
            CreatedByUserId = usuario.UserId,
        };

        await repository.AddAsync(patient);

        return Result<PatientResponseDto>.Ok(new PatientResponseDto
        {
            Id                = patient.Id,
            Name              = patient.Name,
            Email             = patient.Email,
            CPF               = patient.CPF,
            Rg                = patient.Rg,
            Rua               = patient.Rua,
            Numero            = patient.Numero,
            Bairro            = patient.Bairro,
            Cidade            = patient.Cidade,
            Estado            = patient.Estado,
            Cep               = patient.Cep,
            Phone             = patient.Phone,
            IsActive          = patient.IsActive,
            appointmentStatus = AppointmentStatus.Scheduled,
            paymentStatus     = PaymentStatus.Pending,
            CreatedAt         = patient.CreatedAt,
        });
    }

    // ── Atualização ──────────────────────────────────────────────────────────

    public async Task<Result<bool>> UpdateAsync(int id, UpdatePatientDto dto)
    {
        var normalizedEmail = NormalizeOptional(dto.Email);
        var normalizedCpf = DigitsOnly(dto.CPF);

        // Validações de unicidade
        if (await repository.EmailExistsAsync(normalizedEmail, id))
            return Result<bool>.Fail(ErrorCodes.DuplicateEmail, "Email já cadastrado por outro paciente.");

        if (await repository.CpfExistsAsync(normalizedCpf, id))
            return Result<bool>.Fail(ErrorCodes.DuplicateCpf, "CPF já cadastrado por outro paciente.");

        var patient = await repository.GetByIdAsync(id);
        if (patient is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        patient.Name   = NormalizeOptional(dto.Name);
        patient.Email  = normalizedEmail;
        patient.CPF    = normalizedCpf;
        patient.Rg     = NormalizeOptional(dto.Rg);
        patient.Rua    = NormalizeOptional(dto.Rua);
        patient.Numero = NormalizeOptional(dto.Numero);
        patient.Bairro = NormalizeOptional(dto.Bairro);
        patient.Cidade = NormalizeOptional(dto.Cidade);
        patient.Estado = NormalizeOptional(dto.Estado);
        patient.Cep    = DigitsOnly(dto.Cep);
        patient.Phone  = DigitsOnly(dto.Phone);
        patient.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    // ── Toggle Status ────────────────────────────────────────────────────────

    public async Task<Result<bool>> ToggleStatusAsync(int id)
    {
        var patient = await repository.GetByIdAsync(id);
        if (patient is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        patient.IsActive = !patient.IsActive;
        await repository.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    // ── Deleção ──────────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var patient = await repository.GetByIdAsync(id);
        if (patient is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        if (await repository.HasAssociatedRecordsAsync(id))
            return Result<bool>.Fail(ErrorCodes.HasAssociatedRecords, "Não é possível excluir paciente com agendamentos ou pagamentos associados.");

        await repository.DeleteAsync(patient);
        return Result<bool>.Ok(true);
    }
}
