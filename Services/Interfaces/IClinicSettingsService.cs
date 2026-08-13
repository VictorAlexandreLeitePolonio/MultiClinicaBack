using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Clinic;

namespace MultiClinica.API.Services.Interfaces;

public interface IClinicSettingsService
{
    Task<Result<ClinicSettingsDto>> GetCurrentClinicSettingsAsync();
    Task<Result<ClinicSettingsDto>> UpdateCurrentClinicSettingsAsync(UpdateClinicSettingsRequest request);

    Task<Result<ClinicSettingsDto>> GetClinicSettingsAsSuperAdminAsync(int clinicId);
    Task<Result<ClinicSettingsDto>> UpdateClinicSettingsAsSuperAdminAsync(int clinicId, UpdateClinicSettingsRequest request);
}
