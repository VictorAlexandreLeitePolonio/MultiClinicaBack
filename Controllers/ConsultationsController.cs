using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Consultation;
using MultiClinica.API.Services;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Profissional")]
[ApiController]
[Route("api")]
public class ConsultationsController(ConsultationService service) : ControllerBase
{
    [HttpPost("appointments/{id:int}/consultation")]
    [RequireFeature(Features.Transcription)]
    public async Task<IActionResult> Start(int id, [FromBody] StartConsultationDto dto, CancellationToken ct)
    {
        var result = await service.StartAsync(id, dto, ct);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpPut("appointments/{id:int}/consultation/chunks/{n:int}")]
    [RequireFeature(Features.Transcription)]
    [RequestSizeLimit(2 * 1024 * 1024 + 4096)]
    public async Task<IActionResult> SaveChunk(int id, int n, CancellationToken ct)
    {
        try
        {
            var result = await service.SaveChunkAsync(id, n, Request.Body, Request.ContentLength, ct);
            return result.IsSuccess ? NoContent() : Error(result);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            // Kestrel corta o corpo acima do [RequestSizeLimit] no meio da leitura.
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new { code = ErrorCodes.FileTooLarge, message = "O pedaço de áudio excede o limite de 2 MB." });
        }
    }

    [HttpPost("appointments/{id:int}/consultation/finish")]
    [RequireFeature(Features.Transcription)]
    public async Task<IActionResult> Finish(int id, [FromBody] FinishConsultationDto dto, CancellationToken ct)
    {
        var result = await service.FinishAsync(id, dto, ct);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpGet("appointments/{id:int}/consultation")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await service.GetAsync(id);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpPut("appointments/{id:int}/consultation/report")]
    [RequireFeature(Features.Transcription)]
    public async Task<IActionResult> UpdateReport(int id, [FromBody] UpdateConsultationReportDto dto)
    {
        var result = await service.UpdateReportAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpPost("appointments/{id:int}/consultation/review")]
    [RequireFeature(Features.Transcription)]
    public async Task<IActionResult> Review(int id)
    {
        var result = await service.ReviewAsync(id);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpPost("appointments/{id:int}/consultation/retry")]
    [RequireFeature(Features.Transcription)]
    public async Task<IActionResult> Retry(int id)
    {
        var result = await service.RetryAsync(id);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpGet("appointments/{id:int}/consultation/charge-suggestion")]
    public async Task<IActionResult> ChargeSuggestion(int id)
    {
        var result = await service.GetChargeSuggestionAsync(id);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpPost("appointments/{id:int}/consultation/evolution")]
    [RequireFeature(Features.Transcription)]
    public async Task<IActionResult> CreateEvolution(int id)
    {
        var result = await service.CreateEvolutionAsync(id);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    [HttpGet("patients/{patientId:int}/consultation-reports")]
    public async Task<IActionResult> PatientReports(int patientId)
    {
        var result = await service.GetPatientReportsAsync(patientId);
        return result.IsSuccess ? Ok(result.Value) : Error(result);
    }

    private IActionResult Error<T>(Result<T> result) => result.ErrorCode switch
    {
        ErrorCodes.NotFound => NotFound(new { code = result.ErrorCode, message = result.ErrorMessage }),
        ErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden,
            new { code = result.ErrorCode, message = result.ErrorMessage }),
        _ => BadRequest(new { code = result.ErrorCode, message = result.ErrorMessage }),
    };
}
