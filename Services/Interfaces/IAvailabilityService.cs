using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Availability;

namespace MultiClinica.API.Services.Interfaces;

public interface IAvailabilityService
{
    Task<Result<AvailabilitySettingsDto>> GetSettingsAsync();
    Task<Result<AvailabilitySettingsDto>> UpdateSettingsAsync(UpdateAvailabilitySettingsDto dto);
    Task<Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>> GetProfessionalAsync(int professionalId);
    Task<Result<IReadOnlyList<ProfessionalAvailabilityRangeDto>>> ReplaceProfessionalAsync(
        int professionalId, IReadOnlyList<ProfessionalAvailabilityRangeDto> ranges);
    Task<Result<ClinicAvailabilityDto>> GetClinicAvailabilityAsync(int clinicId, DateOnly date);
    Task<Result<int>> ValidateRequestedSlotAsync(int clinicId, DateTimeOffset start);
    Task<Result<bool>> ValidateProfessionalAsync(int clinicId, int professionalId, DateTime startUtc, int durationMinutes);
}
