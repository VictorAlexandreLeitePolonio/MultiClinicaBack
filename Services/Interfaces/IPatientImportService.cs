using Microsoft.AspNetCore.Http;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Patient;

namespace MultiClinica.API.Services.Interfaces;

public interface IPatientImportService
{
    Task<Result<PatientImportResponseDto>> ImportAsync(
        IFormFile? file,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);
}
