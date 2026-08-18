using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

/// <summary>
/// Compõe e envia os e-mails transacionais do paciente (ativação, novo vínculo,
/// reset de senha), gerando o token quando necessário. Retorna se o envio ocorreu;
/// falha de envio nunca deve reverter persistência já feita.
/// </summary>
public interface IPatientNotificationService
{
    Task<bool> SendActivationInviteAsync(PatientAccount account);
    Task<bool> SendNewLinkNoticeAsync(PatientAccount account, string clinicName);
    Task<bool> SendPasswordResetAsync(PatientAccount account);
}
