using MultiClinica.API.Models;

namespace MultiClinica.API.Repositories.Interfaces;

public interface IAppointmentRequestRepository
{
    Task<AppointmentRequest> AddAsync(AppointmentRequest request);

    /// <summary>Request pelo Id, restrito à clínica (isolamento de tenant).</summary>
    Task<AppointmentRequest?> GetForClinicAsync(int id, int clinicaId);

    /// <summary>Request pelo Id, restrito ao paciente autenticado.</summary>
    Task<AppointmentRequest?> GetForPatientAsync(int id, int patientAccountId);

    Task<List<AppointmentRequest>> ListForClinicAsync(int clinicaId);
    Task<List<AppointmentRequest>> ListForPatientAsync(int patientAccountId);

    Task<Clinica?> GetClinicAsync(int clinicId);
    Task<PatientAccount?> GetAccountAsync(int patientAccountId);
    Task<Patient?> GetPatientLinkAsync(int patientAccountId, int clinicaId);
    Task<bool> ProfessionalBelongsToClinicAsync(int professionalId, int clinicaId);

    Task SaveChangesAsync();
}
