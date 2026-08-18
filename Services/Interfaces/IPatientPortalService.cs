using MultiClinica.API.Common;
using MultiClinica.API.DTOs.PatientPortal;

namespace MultiClinica.API.Services.Interfaces;

public interface IPatientPortalService
{
    Task<Result<PatientMeDto>> GetMeAsync();
    Task<Result<PatientMeDto>> UpdateMeAsync(UpdatePatientMeDto dto);
    Task<Result<IReadOnlyList<PatientAppointmentDto>>> GetUpcomingAppointmentsAsync();
    Task<Result<IReadOnlyList<PatientAppointmentDto>>> GetHistoryAppointmentsAsync();
    Task<Result<IReadOnlyList<PatientClinicDto>>> GetClinicsAsync();
}
