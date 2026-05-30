using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.MedicalRecord;

namespace ProjetoLP.API.Services.Interfaces;

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

    Task<Result<string>> UploadContratoAsync(int id, Stream fileStream, string fileName, string contentType);

    Task<Result<string>> UploadExameAsync(int id, Stream fileStream, string fileName, string contentType);

    Task<Result<bool>> DeleteAsync(int id);
}
