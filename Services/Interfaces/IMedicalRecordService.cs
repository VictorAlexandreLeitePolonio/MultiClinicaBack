using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.MedicalRecord;

namespace MultiClinica.API.Services.Interfaces;

public interface IMedicalRecordService
{
    Task<Result<PagedResult<MedicalRecordResponseDto>>> GetPagedAsync(
        int? patientId,
        string? patientName,
        int? userId,
        DateOnly? createdAt,
        int page,
        int pageSize);

    Task<Result<MedicalRecordResponseDto>> GetByIdAsync(int id);

    Task<Result<MedicalRecordResponseDto>> CreateAsync(CreateMedicalRecordDto dto);

    Task<Result<MedicalRecordResponseDto>> UpdateAsync(int id, UpdateMedicalRecordDto dto);

    Task<Result<bool>> DeleteAsync(int id);
}
