using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Patient;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

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

    Task<Result<PatientCreatedResponseDto>> CreateAsync(CreatePatientDto dto);

    /// <summary>Provisiona acesso ao portal para um paciente legado (sem identidade global).</summary>
    Task<Result<PatientCreatedResponseDto>> ProvisionPortalAccessAsync(int id);

    /// <summary>Reenvia o convite de ativação para um paciente com conta pendente.</summary>
    Task<Result<PatientCreatedResponseDto>> ResendPortalInviteAsync(int id);

    Task<Result<bool>> UpdateAsync(int id, UpdatePatientDto dto);

    Task<Result<bool>> ToggleStatusAsync(int id);

    Task<Result<bool>> DeleteAsync(int id);
}
