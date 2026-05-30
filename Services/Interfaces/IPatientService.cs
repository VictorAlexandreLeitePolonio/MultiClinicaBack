using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Patient;
using ProjetoLP.API.Models;

namespace ProjetoLP.API.Services.Interfaces;

public interface IPatientService
{
    Task<Result<PagedResult<PatientResponseDto>>> GetPagedAsync(
        string? name,
        bool? isActive,
        AppointmentStatus? appointmentStatus,
        PaymentStatus? paymentStatus,
        int page,
        int pageSize);

    Task<Result<PatientResponseDto>> GetByIdAsync(int id);

    Task<Result<PatientProfileDto>> GetProfileAsync(int id);

    Task<Result<PatientResponseDto>> CreateAsync(CreatePatientDto dto);

    Task<Result<bool>> UpdateAsync(int id, UpdatePatientDto dto);

    Task<Result<bool>> ToggleStatusAsync(int id);

    Task<Result<bool>> DeleteAsync(int id);
}
