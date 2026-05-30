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
}
