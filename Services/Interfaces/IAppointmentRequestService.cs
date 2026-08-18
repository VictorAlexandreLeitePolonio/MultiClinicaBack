using MultiClinica.API.Common;
using MultiClinica.API.DTOs.AppointmentRequest;

namespace MultiClinica.API.Services.Interfaces;

public interface IAppointmentRequestService
{
    // Paciente
    Task<Result<AppointmentRequestDto>> CreateAsync(CreateAppointmentRequestDto dto);
    Task<Result<IReadOnlyList<AppointmentRequestDto>>> ListForPatientAsync();
    Task<Result<AppointmentRequestDto>> CancelByPatientAsync(int id, ReasonDto dto);

    // Clínica
    Task<Result<IReadOnlyList<AppointmentRequestDto>>> ListForClinicAsync();
    Task<Result<AppointmentRequestDto>> GetForClinicAsync(int id);
    Task<Result<AppointmentRequestDto>> AcceptAsync(int id, AcceptAppointmentRequestDto dto);
    Task<Result<AppointmentRequestDto>> RejectAsync(int id, ReasonDto dto);
    Task<Result<AppointmentRequestDto>> CancelByClinicAsync(int id, ReasonDto dto);
}
