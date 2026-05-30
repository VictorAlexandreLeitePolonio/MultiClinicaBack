using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.MedicalRecord;
using ProjetoLP.API.Services.Interfaces;

namespace ProjetoLP.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MedicalRecordsController(IMedicalRecordService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMedicalRecords(
        [FromQuery] int? patientId,
        [FromQuery] string? patientName,
        [FromQuery] int? userId,
        [FromQuery] DateOnly? createdAt,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await service.GetPagedAsync(
            patientId, patientName, userId, createdAt, page, pageSize);
        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMedicalRecord(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreateMedicalRecord(CreateMedicalRecordDto dto)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetMedicalRecord), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPost("{id}/contrato")]
    public async Task<IActionResult> UploadContrato(int id, IFormFile file)
    {
        if (file == null)
            return BadRequest(new { message = "Arquivo não fornecido." });

        using var stream = file.OpenReadStream();
        var result = await service.UploadContratoAsync(id, stream, file.FileName, file.ContentType);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound         => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.InvalidFileType  => BadRequest(new { message = result.ErrorMessage }),
                ErrorCodes.FileTooLarge     => BadRequest(new { message = result.ErrorMessage }),
                _                           => BadRequest(new { message = result.ErrorMessage })
            };

        return Ok(new { url = result.Value });
    }

    [HttpPost("{id}/exames")]
    public async Task<IActionResult> UploadExame(int id, IFormFile file)
    {
        if (file == null)
            return BadRequest(new { message = "Arquivo não fornecido." });

        using var stream = file.OpenReadStream();
        var result = await service.UploadExameAsync(id, stream, file.FileName, file.ContentType);

        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound         => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.InvalidFileType  => BadRequest(new { message = result.ErrorMessage }),
                ErrorCodes.FileTooLarge     => BadRequest(new { message = result.ErrorMessage }),
                _                           => BadRequest(new { message = result.ErrorMessage })
            };

        return Ok(new { url = result.Value });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMedicalRecord(int id, UpdateMedicalRecordDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMedicalRecord(int id)
    {
        var result = await service.DeleteAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });

        return NoContent();
    }
}
