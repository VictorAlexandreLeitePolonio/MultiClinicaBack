using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Evolution;

namespace MultiClinica.API.Services.Interfaces;

public interface IEvolutionService
{
    Task<Result<List<EvolutionTemplateFieldResponseDto>>> GetFieldsAsync(int templateId);
    Task<Result<EvolutionTemplateFieldResponseDto>> CreateFieldAsync(int templateId, CreateEvolutionTemplateFieldDto dto);
    Task<Result<bool>> UpdateFieldAsync(int fieldId, UpdateEvolutionTemplateFieldDto dto);
    Task<Result<bool>> DeactivateFieldAsync(int fieldId);
    Task<Result<PagedResult<PatientEvolutionResponseDto>>> GetEvolutionsAsync(int patientId, int? appointmentId, int page, int pageSize);
    Task<Result<PatientEvolutionResponseDto>> GetEvolutionAsync(int patientId, int evolutionId);
    Task<Result<PatientEvolutionResponseDto>> CreateEvolutionAsync(int patientId, CreatePatientEvolutionDto dto);
    Task<Result<bool>> UpdateEvolutionAsync(int patientId, int evolutionId, UpdatePatientEvolutionDto dto);
    Task<Result<bool>> DeleteEvolutionAsync(int patientId, int evolutionId);
}
