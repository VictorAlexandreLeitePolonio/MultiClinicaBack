using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Patient;

/// <summary>
/// Resposta do cadastro/provisionamento de paciente, expondo a identidade
/// global resolvida e o resultado do vínculo com a clínica.
/// </summary>
public class PatientCreatedResponseDto
{
    /// <summary>Id do registro clínico (Patient) desta clínica.</summary>
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int PatientAccountId { get; set; }
    public PatientAccountStatus PatientAccountStatus { get; set; }
    public PatientPortalLinkResult LinkResult { get; set; }

    /// <summary>
    /// Se um convite de acesso ao portal foi disparado. Em BACK-1 é sempre
    /// <c>false</c> (stub); o envio real é implementado em BACK-2.
    /// </summary>
    public bool InvitationSent { get; set; }
}
