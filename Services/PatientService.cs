using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Patient;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class PatientService(
    IPatientRepository repository,
    IPatientAccountService accountService,
    IUsuarioLogadoService usuario) : IPatientService
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

    public async Task<Result<PatientCreatedResponseDto>> CreateAsync(CreatePatientDto dto)
    {
        var email = accountService.NormalizeEmail(dto.Email);
        var cpf   = accountService.NormalizeCpf(dto.CPF);
        var phone = DigitsOnly(dto.Phone);

        // Novo contrato: e-mail, CPF e telefone obrigatórios para todo novo paciente.
        if (email is null)
            return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.EmptyField, "E-mail é obrigatório.");
        if (cpf is null)
            return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.EmptyField, "CPF é obrigatório.");
        if (phone is null)
            return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.EmptyField, "Telefone é obrigatório.");

        // Resolve a identidade global pelo e-mail (chave de identidade da pessoa).
        var account = await accountService.FindByEmailAsync(email);
        PatientPortalLinkResult linkResult;

        if (account is not null)
        {
            // Conta já existe globalmente: no máximo um Patient por (conta, clínica).
            if (await repository.LinkExistsAsync(account.Id))
                return Result<PatientCreatedResponseDto>.Fail(
                    ErrorCodes.AlreadyLinked, "Este paciente já está vinculado a esta clínica.");

            linkResult = PatientPortalLinkResult.LinkedExistingAccount;
        }
        else
        {
            // Conta nova: preserva unicidade dentro da clínica + unicidade global de CPF.
            if (await repository.EmailExistsAsync(email))
                return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.DuplicateEmail, "Email já cadastrado por outro paciente.");
            if (await repository.CpfExistsAsync(cpf))
                return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.DuplicateCpf, "CPF já cadastrado por outro paciente.");
            if (await accountService.CpfExistsAsync(cpf))
                return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.DuplicateCpf, "CPF já vinculado a outra conta global.");

            account = accountService.CreatePending(NormalizeOptional(dto.Name), email, cpf, phone, usuario.UserId);
            linkResult = PatientPortalLinkResult.CreatedAccount;
        }

        var patient = new Patient
        {
            ClinicaId = usuario.ClinicaId,
            Name   = NormalizeOptional(dto.Name),
            Email  = email,
            CPF    = cpf,
            Rg     = NormalizeOptional(dto.Rg),
            Rua    = NormalizeOptional(dto.Rua),
            Numero = NormalizeOptional(dto.Numero),
            Bairro = NormalizeOptional(dto.Bairro),
            Cidade = NormalizeOptional(dto.Cidade),
            Estado = NormalizeOptional(dto.Estado),
            Cep    = DigitsOnly(dto.Cep),
            Phone  = phone,
            CreatedByUserId = usuario.UserId,
        };

        // Vínculo identidade + registro clínico num único SaveChanges (atômico).
        if (linkResult == PatientPortalLinkResult.CreatedAccount)
            patient.PatientAccount = account;   // insere a conta em cascata
        else
            patient.PatientAccountId = account.Id;

        await repository.AddAsync(patient);

        return Result<PatientCreatedResponseDto>.Ok(new PatientCreatedResponseDto
        {
            Id                   = patient.Id,
            PatientId            = patient.Id,
            PatientAccountId     = account.Id,
            PatientAccountStatus = account.Status,
            LinkResult           = linkResult,
            InvitationSent       = false, // stub — envio real do convite em BACK-2
        });
    }

    // ── Provisionamento de acesso (pacientes legados) ────────────────────────

    public async Task<Result<PatientCreatedResponseDto>> ProvisionPortalAccessAsync(int id)
    {
        var patient = await repository.GetByIdAsync(id);
        if (patient is null)
            return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        if (patient.PatientAccountId is not null)
            return Result<PatientCreatedResponseDto>.Fail(
                ErrorCodes.AlreadyLinked, "Paciente já possui identidade global vinculada.");

        var email = accountService.NormalizeEmail(patient.Email);
        if (email is null)
            return Result<PatientCreatedResponseDto>.Fail(
                ErrorCodes.EmptyField, "Paciente sem e-mail: cadastre um e-mail antes de provisionar o acesso ao portal.");

        var cpf = accountService.NormalizeCpf(patient.CPF);
        var account = await accountService.FindByEmailAsync(email);
        PatientPortalLinkResult linkResult;

        if (account is not null)
        {
            if (await repository.LinkExistsAsync(account.Id))
                return Result<PatientCreatedResponseDto>.Fail(
                    ErrorCodes.AlreadyLinked, "Esta clínica já possui um paciente vinculado a esta conta.");

            patient.PatientAccountId = account.Id;
            linkResult = PatientPortalLinkResult.LinkedExistingAccount;
        }
        else
        {
            if (await accountService.CpfExistsAsync(cpf))
                return Result<PatientCreatedResponseDto>.Fail(ErrorCodes.DuplicateCpf, "CPF já vinculado a outra conta global.");

            account = accountService.CreatePending(patient.Name, email, cpf, patient.Phone, usuario.UserId);
            patient.PatientAccount = account;
            linkResult = PatientPortalLinkResult.CreatedAccount;
        }

        patient.Email = email; // normaliza o e-mail do registro clínico ao provisionar
        patient.UpdatedByUserId = usuario.UserId;
        await repository.SaveChangesAsync();

        return Result<PatientCreatedResponseDto>.Ok(new PatientCreatedResponseDto
        {
            Id                   = patient.Id,
            PatientId            = patient.Id,
            PatientAccountId     = account.Id,
            PatientAccountStatus = account.Status,
            LinkResult           = linkResult,
            InvitationSent       = false, // stub — envio real do convite em BACK-2
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
