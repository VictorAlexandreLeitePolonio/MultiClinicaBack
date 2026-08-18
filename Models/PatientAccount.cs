namespace MultiClinica.API.Models;

/// <summary>Estado de ativação da identidade global do paciente.</summary>
public enum PatientAccountStatus
{
    PendingActivation,
    Active,
    Inactive
}

/// <summary>
/// Identidade global do paciente (a pessoa), independente de clínica.
/// Nunca possui <c>ClinicaId</c>: uma mesma conta pode estar vinculada a
/// várias clínicas através de múltiplos registros <see cref="Patient"/>.
/// A senha nunca é definida pela clínica — o acesso é ativado pelo próprio
/// paciente via convite (fluxo tratado em BACK-2).
/// </summary>
public class PatientAccount : AuditableEntity
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? CPF { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public PatientAccountStatus Status { get; set; } = PatientAccountStatus.PendingActivation;
    public DateTime? ActivatedAt { get; set; }

    public ICollection<Patient> Patients { get; set; } = [];
}

/// <summary>Resultado da resolução de identidade + vínculo do paciente com a clínica.</summary>
public enum PatientPortalLinkResult
{
    /// <summary>Conta global criada agora + vínculo com a clínica.</summary>
    CreatedAccount,
    /// <summary>Conta global já existia (outra clínica) e foi apenas vinculada.</summary>
    LinkedExistingAccount,
    /// <summary>Esta clínica já possui um paciente vinculado a esta conta.</summary>
    AlreadyLinked
}
