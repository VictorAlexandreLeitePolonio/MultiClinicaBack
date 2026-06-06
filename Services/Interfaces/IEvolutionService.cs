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
    Task<Result<List<PatientTreatmentResponseDto>>> GetTreatmentsAsync(int patientId);
    Task<Result<PatientTreatmentResponseDto>> GetTreatmentAsync(int patientId, int treatmentId);
    Task<Result<PatientTreatmentResponseDto>> CreateTreatmentAsync(int patientId, CreatePatientTreatmentDto dto);
    Task<Result<bool>> UpdateTreatmentAsync(int patientId, int treatmentId, UpdatePatientTreatmentDto dto);
    Task<Result<PagedResult<PatientEvolutionResponseDto>>> GetEvolutionsAsync(int patientId, int treatmentId, int page, int pageSize);
    Task<Result<PatientEvolutionResponseDto>> GetEvolutionAsync(int patientId, int treatmentId, int evolutionId);
    Task<Result<TreatmentProgressResponseDto>> GetTreatmentProgressAsync(int patientId, int treatmentId);
    Task<Result<PatientEvolutionResponseDto>> CreateEvolutionAsync(int patientId, int treatmentId, CreatePatientEvolutionDto dto);
    Task<Result<bool>> UpdateEvolutionAsync(int patientId, int treatmentId, int evolutionId, UpdatePatientEvolutionDto dto);
    Task<Result<bool>> DeleteEvolutionAsync(int patientId, int treatmentId, int evolutionId);
}
