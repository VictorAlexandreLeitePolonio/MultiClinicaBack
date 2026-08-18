using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[ApiController]
[Route("api/clinic/media")]
[Authorize(Roles = "Administrador")]
public class ClinicMediaController(
    AppDbContext db,
    IUsuarioLogadoService usuario,
    IAttachmentStorage storage) : ControllerBase
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private static readonly TimeSpan UrlTtl = TimeSpan.FromHours(1);

    // Content types públicos aceitos e sua extensão canônica.
    private static readonly Dictionary<string, string> AllowedTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"]  = ".png",
        ["image/webp"] = ".webp",
    };

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var media = await db.ClinicMedia
            .Where(m => m.ClinicaId == usuario.ClinicaId && !m.IsDeleted)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        var result = new List<ClinicMediaDto>();
        foreach (var m in media)
            result.Add(await MapAsync(m));
        return Ok(result);
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] ClinicMediaType type,
        [FromForm] int? sortOrder,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Arquivo vazio." });
        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "Arquivo muito grande. Máximo permitido: 5MB." });
        if (!AllowedTypes.TryGetValue(file.ContentType, out var ext))
            return BadRequest(new { message = "Tipo de arquivo inválido. Aceitos: JPEG, PNG, WEBP." });

        var objectKey = $"clinicas/{usuario.ClinicaId}/public/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var savedKey = await storage.SaveAsync(stream, objectKey, file.ContentType, cancellationToken);

        var media = new ClinicMedia
        {
            ClinicaId       = usuario.ClinicaId,
            ObjectKey       = savedKey,
            Type            = type,
            SortOrder       = sortOrder ?? 0,
            ContentType     = file.ContentType,
            Size            = file.Length,
            CreatedByUserId = usuario.UserId,
        };
        db.ClinicMedia.Add(media);
        await db.SaveChangesAsync(cancellationToken);

        return Ok(await MapAsync(media));
    }

    [HttpPatch("{id}/order")]
    public async Task<IActionResult> Reorder(int id, UpdateMediaOrderRequest request)
    {
        var media = await Find(id);
        if (media is null)
            return NotFound(new { message = "Mídia não encontrada." });

        media.SortOrder = request.SortOrder;
        media.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
        return Ok(await MapAsync(media));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var media = await Find(id);
        if (media is null)
            return NotFound(new { message = "Mídia não encontrada." });

        media.IsDeleted = true;
        media.DeletedAt = DateTime.UtcNow;
        media.DeletedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Task<ClinicMedia?> Find(int id)
        => db.ClinicMedia.FirstOrDefaultAsync(m =>
            m.Id == id && m.ClinicaId == usuario.ClinicaId && !m.IsDeleted);

    private async Task<ClinicMediaDto> MapAsync(ClinicMedia m) => new()
    {
        Id = m.Id,
        Type = m.Type,
        SortOrder = m.SortOrder,
        Url = await storage.CreateReadUrlAsync(m.ObjectKey, UrlTtl),
    };
}
