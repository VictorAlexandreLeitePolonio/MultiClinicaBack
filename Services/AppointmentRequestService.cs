using MultiClinica.API.Common;
using MultiClinica.API.DTOs.AppointmentRequest;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class AppointmentRequestService(
    IAppointmentRequestRepository repository,
    IPatientAccountLoggedService patient,
    IUsuarioLogadoService usuario,
    IPatientNotificationService notifications) : IAppointmentRequestService
{
    // ── Paciente ─────────────────────────────────────────────────────────────

    public async Task<Result<AppointmentRequestDto>> CreateAsync(CreateAppointmentRequestDto dto)
    {
        var clinic = await repository.GetClinicAsync(dto.ClinicId);
        if (clinic is null || !clinic.IsActive)
            return Fail(ErrorCodes.NotFound, "Clínica não encontrada ou inativa.");

        if (!clinic.AcceptsAppointmentRequests)
            return Fail(ErrorCodes.RequestsDisabled, "Esta clínica não aceita solicitações de consulta.");

        if (dto.RequestedDate <= DateTime.UtcNow)
            return Fail(ErrorCodes.InvalidDate, "A data solicitada deve ser futura.");

        // MVP: paciente precisa já estar vinculado à clínica.
        var link = await repository.GetPatientLinkAsync(patient.PatientAccountId, dto.ClinicId);
        if (link is null)
            return Fail(ErrorCodes.NotLinked, "Você não possui vínculo com esta clínica.");

        var request = new AppointmentRequest
        {
            PatientAccountId = patient.PatientAccountId,
            ClinicaId        = dto.ClinicId,
            RequestedDate    = dto.RequestedDate,
            Reason           = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim(),
            Status           = AppointmentRequestStatus.Pending,
        };
        await repository.AddAsync(request);

        var account = await repository.GetAccountAsync(patient.PatientAccountId);
        if (account is not null)
            await notifications.NotifyRequestCreatedAsync(clinic, account, request);

        request.Clinica = clinic;
        return Result<AppointmentRequestDto>.Ok(MapDto(request));
    }

    public async Task<Result<IReadOnlyList<AppointmentRequestDto>>> ListForPatientAsync()
    {
        var items = await repository.ListForPatientAsync(patient.PatientAccountId);
        return Result<IReadOnlyList<AppointmentRequestDto>>.Ok(items.Select(MapDto).ToList());
    }

    public async Task<Result<AppointmentRequestDto>> CancelByPatientAsync(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Fail(ErrorCodes.EmptyField, "O motivo do cancelamento é obrigatório.");

        var request = await repository.GetForPatientAsync(id, patient.PatientAccountId);
        if (request is null)
            return Fail(ErrorCodes.NotFound, "Solicitação não encontrada.");

        if (request.Status != AppointmentRequestStatus.Pending)
            return Fail(ErrorCodes.InvalidStatus, "Somente solicitações pendentes podem ser canceladas.");

        ApplyTermination(request, AppointmentRequestStatus.Cancelled, dto.Reason, CancellationOrigin.Patient, byUserId: null);
        await repository.SaveChangesAsync();

        var account = await repository.GetAccountAsync(patient.PatientAccountId);
        if (account is not null)
            await notifications.NotifyRequestCancelledAsync(account, request.Clinica, request);

        return Result<AppointmentRequestDto>.Ok(MapDto(request));
    }

    // ── Clínica ──────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<AppointmentRequestDto>>> ListForClinicAsync()
    {
        var items = await repository.ListForClinicAsync(usuario.ClinicaId);
        return Result<IReadOnlyList<AppointmentRequestDto>>.Ok(items.Select(MapDto).ToList());
    }

    public async Task<Result<AppointmentRequestDto>> GetForClinicAsync(int id)
    {
        var request = await repository.GetForClinicAsync(id, usuario.ClinicaId);
        return request is null
            ? Fail(ErrorCodes.NotFound, "Solicitação não encontrada.")
            : Result<AppointmentRequestDto>.Ok(MapDto(request));
    }

    public async Task<Result<AppointmentRequestDto>> AcceptAsync(int id, AcceptAppointmentRequestDto dto)
    {
        var request = await repository.GetForClinicAsync(id, usuario.ClinicaId);
        if (request is null)
            return Fail(ErrorCodes.NotFound, "Solicitação não encontrada.");

        if (request.Status != AppointmentRequestStatus.Pending)
            return Fail(ErrorCodes.InvalidStatus, "Somente solicitações pendentes podem ser aceitas.");

        if (!await repository.ProfessionalBelongsToClinicAsync(dto.ProfessionalId, usuario.ClinicaId))
            return Fail(ErrorCodes.Forbidden, "Profissional inválido para esta clínica.");

        var account = request.PatientAccount ?? await repository.GetAccountAsync(request.PatientAccountId);

        // Vínculo Patient da clínica (cria se necessário) — tudo num único SaveChanges.
        var link = await repository.GetPatientLinkAsync(request.PatientAccountId, usuario.ClinicaId);
        link ??= new Patient
        {
            ClinicaId        = usuario.ClinicaId,
            PatientAccountId = request.PatientAccountId,
            Name             = account?.Name,
            Email            = account?.Email,
            CPF              = account?.CPF,
            Phone            = account?.Phone,
            CreatedByUserId  = usuario.UserId,
        };

        var appointment = new Appointment
        {
            ClinicaId       = usuario.ClinicaId,
            UserId          = dto.ProfessionalId,
            Patient         = link,
            AppointmentDate = request.RequestedDate,
            Status          = AppointmentStatus.Scheduled,
            CreatedByUserId = usuario.UserId,
        };

        request.Appointment = appointment; // FK AppointmentId preenchida no save
        request.Status      = AppointmentRequestStatus.Accepted;
        request.RespondedAt = DateTime.UtcNow;
        request.UpdatedByUserId = usuario.UserId;

        await repository.SaveChangesAsync();

        if (account is not null)
            await notifications.NotifyRequestAcceptedAsync(account, request.Clinica, request);

        return Result<AppointmentRequestDto>.Ok(MapDto(request));
    }

    public async Task<Result<AppointmentRequestDto>> RejectAsync(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Fail(ErrorCodes.EmptyField, "O motivo da recusa é obrigatório.");

        var request = await repository.GetForClinicAsync(id, usuario.ClinicaId);
        if (request is null)
            return Fail(ErrorCodes.NotFound, "Solicitação não encontrada.");

        if (request.Status != AppointmentRequestStatus.Pending)
            return Fail(ErrorCodes.InvalidStatus, "Somente solicitações pendentes podem ser recusadas.");

        ApplyTermination(request, AppointmentRequestStatus.Rejected, dto.Reason, cancelledBy: null, byUserId: usuario.UserId);
        await repository.SaveChangesAsync();

        if (request.PatientAccount is not null)
            await notifications.NotifyRequestRejectedAsync(request.PatientAccount, request.Clinica, request);

        return Result<AppointmentRequestDto>.Ok(MapDto(request));
    }

    public async Task<Result<AppointmentRequestDto>> CancelByClinicAsync(int id, ReasonDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Fail(ErrorCodes.EmptyField, "O motivo do cancelamento é obrigatório.");

        var request = await repository.GetForClinicAsync(id, usuario.ClinicaId);
        if (request is null)
            return Fail(ErrorCodes.NotFound, "Solicitação não encontrada.");

        if (request.Status != AppointmentRequestStatus.Pending)
            return Fail(ErrorCodes.InvalidStatus, "Somente solicitações pendentes podem ser canceladas.");

        ApplyTermination(request, AppointmentRequestStatus.Cancelled, dto.Reason, CancellationOrigin.Clinic, usuario.UserId);
        await repository.SaveChangesAsync();

        if (request.PatientAccount is not null)
            await notifications.NotifyRequestCancelledAsync(request.PatientAccount, request.Clinica, request);

        return Result<AppointmentRequestDto>.Ok(MapDto(request));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void ApplyTermination(AppointmentRequest request, AppointmentRequestStatus status,
        string? reason, CancellationOrigin? cancelledBy, int? byUserId)
    {
        request.Status         = status;
        request.ResponseReason = reason?.Trim();
        request.CancelledBy    = cancelledBy;
        request.RespondedAt    = DateTime.UtcNow;
        request.UpdatedByUserId = byUserId;
    }

    private static Result<AppointmentRequestDto> Fail(string code, string message)
        => Result<AppointmentRequestDto>.Fail(code, message);

    private static AppointmentRequestDto MapDto(AppointmentRequest r) => new()
    {
        Id               = r.Id,
        PatientAccountId = r.PatientAccountId,
        ClinicId         = r.ClinicaId,
        ClinicName       = r.Clinica is null ? null
            : string.IsNullOrWhiteSpace(r.Clinica.NomeFantasia) ? r.Clinica.Nome : r.Clinica.NomeFantasia,
        PatientName      = r.PatientAccount?.Name,
        RequestedDate    = r.RequestedDate,
        Reason           = r.Reason,
        Status           = r.Status,
        ResponseReason   = r.ResponseReason,
        CancelledBy      = r.CancelledBy,
        RespondedAt      = r.RespondedAt,
        AppointmentId    = r.AppointmentId,
        CreatedAt        = r.CreatedAt,
    };
}
