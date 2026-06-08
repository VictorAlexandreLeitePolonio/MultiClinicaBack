using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Attachment;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Profissional")]
[ApiController]
[Route("api/attachments")]
public class AttachmentsController(
    AppDbContext db,
    IUsuarioLogadoService usuario,
    IAttachmentStorage storage) : ControllerBase
{
    private const long MaxFileSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> ContractContentTypes = ["application/pdf"];
    private static readonly HashSet<string> ExamContentTypes = ["image/jpeg", "image/png"];

    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] int patientId,
        [FromForm] int? medicalRecordId,
        [FromForm] ClinicalAttachmentType type,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest(new { message = "Arquivo vazio." });

        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "Arquivo muito grande. Máximo permitido: 10MB." });

        if (!IsAllowedFile(type, file))
            return BadRequest(new { message = "Tipo de arquivo inválido para o anexo informado." });

        var patient = await db.Patients.FirstOrDefaultAsync(p =>
            p.Id == patientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted, cancellationToken);
        if (patient is null)
            return NotFound(new { message = "Paciente não encontrado." });

        MedicalRecord? medicalRecord = null;
        if (medicalRecordId.HasValue)
        {
            medicalRecord = await db.MedicalRecords.FirstOrDefaultAsync(m =>
                m.Id == medicalRecordId.Value
                && m.PatientId == patientId
                && m.ClinicaId == usuario.ClinicaId
                && !m.IsDeleted, cancellationToken);

            if (medicalRecord is null)
                return NotFound(new { message = "Prontuário não encontrado." });
        }

        var objectKey = $"clinicas/{usuario.ClinicaId}/patients/{patientId}/{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
        await using var stream = file.OpenReadStream();
        var savedKey = await storage.SaveAsync(stream, objectKey, file.ContentType, cancellationToken);

        var attachment = new ClinicalAttachment
        {
            ClinicaId = usuario.ClinicaId,
            PatientId = patient.Id,
            MedicalRecordId = medicalRecord?.Id,
            Type = type,
            OriginalFileName = file.FileName,
            ObjectKey = savedKey,
            ContentType = file.ContentType,
            Size = file.Length,
            UploadedByUserId = usuario.UserId,
            UploadedAt = DateTime.UtcNow,
            CreatedByUserId = usuario.UserId
        };

        db.ClinicalAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);

        return Created($"/api/attachments/{attachment.Id}", ToDto(attachment));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var attachment = await db.ClinicalAttachments.FirstOrDefaultAsync(a =>
            a.Id == id && a.ClinicaId == usuario.ClinicaId && !a.IsDeleted);

        return attachment is null
            ? NotFound(new { message = "Anexo não encontrado." })
            : Ok(ToDto(attachment));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> CreateDownloadUrl(int id, CancellationToken cancellationToken)
    {
        var attachment = await db.ClinicalAttachments.FirstOrDefaultAsync(a =>
            a.Id == id && a.ClinicaId == usuario.ClinicaId && !a.IsDeleted, cancellationToken);

        if (attachment is null)
            return NotFound(new { message = "Anexo não encontrado." });

        var url = await storage.CreateReadUrlAsync(attachment.ObjectKey, TimeSpan.FromMinutes(10), cancellationToken);
        return Ok(new { url, expiresInSeconds = 600 });
    }

    private static AttachmentResponseDto ToDto(ClinicalAttachment attachment) => new()
    {
        Id = attachment.Id,
        PatientId = attachment.PatientId,
        MedicalRecordId = attachment.MedicalRecordId,
        Type = attachment.Type,
        OriginalFileName = attachment.OriginalFileName,
        ObjectKey = attachment.ObjectKey,
        ContentType = attachment.ContentType,
        Size = attachment.Size,
        UploadedByUserId = attachment.UploadedByUserId,
        UploadedAt = attachment.UploadedAt
    };

    private static bool IsAllowedFile(ClinicalAttachmentType type, IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        return type switch
        {
            ClinicalAttachmentType.Contract =>
                ContractContentTypes.Contains(file.ContentType) && extension == ".pdf",
            ClinicalAttachmentType.Exam =>
                ExamContentTypes.Contains(file.ContentType) && (extension == ".jpg" || extension == ".jpeg" || extension == ".png"),
            ClinicalAttachmentType.Other => false,
            _ => false
        };
    }
}
