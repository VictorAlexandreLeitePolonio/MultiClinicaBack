using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ClinicProfileService(
    AppDbContext db,
    IUsuarioLogadoService usuario,
    IAttachmentStorage storage) : IClinicProfileService
{
    private static readonly TimeSpan MediaUrlTtl = TimeSpan.FromHours(1);

    // ── Categorias ─────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<ClinicCategoryDto>>> GetCategoryCatalogAsync()
    {
        var items = await db.ClinicCategories
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new ClinicCategoryDto { Id = c.Id, Name = c.Name, Slug = c.Slug })
            .ToListAsync();
        return Result<IReadOnlyList<ClinicCategoryDto>>.Ok(items);
    }

    public async Task<Result<IReadOnlyList<ClinicCategoryDto>>> GetClinicCategoriesAsync()
    {
        var clinic = await db.Clinicas.Include(c => c.Categories)
            .FirstOrDefaultAsync(c => c.Id == usuario.ClinicaId && !c.IsDeleted);
        if (clinic is null)
            return Result<IReadOnlyList<ClinicCategoryDto>>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        return Result<IReadOnlyList<ClinicCategoryDto>>.Ok(MapCategories(clinic.Categories));
    }

    public async Task<Result<IReadOnlyList<ClinicCategoryDto>>> SetClinicCategoriesAsync(SetClinicCategoriesRequest request)
    {
        var clinic = await db.Clinicas.Include(c => c.Categories)
            .FirstOrDefaultAsync(c => c.Id == usuario.ClinicaId && !c.IsDeleted);
        if (clinic is null)
            return Result<IReadOnlyList<ClinicCategoryDto>>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        var ids = request.CategoryIds.Distinct().ToList();
        var categories = await db.ClinicCategories
            .Where(c => ids.Contains(c.Id) && c.IsActive && !c.IsDeleted)
            .ToListAsync();

        if (categories.Count != ids.Count)
            return Result<IReadOnlyList<ClinicCategoryDto>>.Fail(ErrorCodes.InvalidValue, "Uma ou mais categorias são inválidas.");

        clinic.Categories.Clear();
        foreach (var c in categories) clinic.Categories.Add(c);
        await db.SaveChangesAsync();

        return Result<IReadOnlyList<ClinicCategoryDto>>.Ok(MapCategories(clinic.Categories));
    }

    // ── Horários ───────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<BusinessHourDto>>> GetBusinessHoursAsync()
    {
        var items = await LoadHours(usuario.ClinicaId);
        return Result<IReadOnlyList<BusinessHourDto>>.Ok(items.Select(MapHour).ToList());
    }

    public async Task<Result<BusinessHourDto>> AddBusinessHourAsync(CreateBusinessHourRequest request)
    {
        if (request.StartTime >= request.EndTime)
            return Result<BusinessHourDto>.Fail(ErrorCodes.InvalidValue, "O horário inicial deve ser menor que o final.");

        var sameDay = await db.ClinicBusinessHours
            .Where(h => h.ClinicaId == usuario.ClinicaId && h.DayOfWeek == request.DayOfWeek && !h.IsDeleted)
            .ToListAsync();

        // Sobreposição: faixas se cruzam quando start < outroEnd && end > outroStart.
        if (sameDay.Any(h => request.StartTime < h.EndTime && request.EndTime > h.StartTime))
            return Result<BusinessHourDto>.Fail(ErrorCodes.InvalidValue, "A faixa de horário se sobrepõe a uma já existente.");

        var hour = new ClinicBusinessHour
        {
            ClinicaId = usuario.ClinicaId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            CreatedByUserId = usuario.UserId,
        };
        db.ClinicBusinessHours.Add(hour);
        await db.SaveChangesAsync();

        return Result<BusinessHourDto>.Ok(MapHour(hour));
    }

    public async Task<Result<bool>> DeleteBusinessHourAsync(int id)
    {
        var hour = await db.ClinicBusinessHours
            .FirstOrDefaultAsync(h => h.Id == id && h.ClinicaId == usuario.ClinicaId && !h.IsDeleted);
        if (hour is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Faixa de horário não encontrada.");

        db.ClinicBusinessHours.Remove(hour);
        await db.SaveChangesAsync();
        return Result<bool>.Ok(true);
    }

    // ── Perfil público ─────────────────────────────────────────────────────────

    public async Task<Result<PublicClinicDto>> GetPublicBySlugAsync(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var clinic = await db.Clinicas
            .Include(c => c.Categories)
            .Include(c => c.Media)
            .Include(c => c.BusinessHours)
            .FirstOrDefaultAsync(c => c.PublicSlug == normalized && c.IsPublic && c.IsActive && !c.IsDeleted);

        if (clinic is null)
            return Result<PublicClinicDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        var media = clinic.Media.Where(m => !m.IsDeleted).OrderBy(m => m.SortOrder).ToList();
        var cover = media.FirstOrDefault(m => m.Type == ClinicMediaType.Cover);

        var gallery = new List<string>();
        foreach (var m in media.Where(m => m.Type == ClinicMediaType.Gallery))
            gallery.Add(await storage.CreateReadUrlAsync(m.ObjectKey, MediaUrlTtl));

        var dto = new PublicClinicDto
        {
            Id          = clinic.Id,
            Slug        = clinic.PublicSlug,
            DisplayName = string.IsNullOrWhiteSpace(clinic.DisplayName)
                ? (string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia)
                : clinic.DisplayName,
            Description = clinic.Description,
            LogoUrl     = clinic.LogoUrl,
            CoverUrl    = cover is null ? null : await storage.CreateReadUrlAsync(cover.ObjectKey, MediaUrlTtl),
            Gallery     = gallery,
            Categories  = MapCategories(clinic.Categories),
            Address     = new ClinicAddressDto
            {
                Rua = clinic.Rua, Numero = clinic.Numero, Bairro = clinic.Bairro,
                Cidade = clinic.Cidade, Estado = clinic.Estado, Cep = clinic.Cep,
            },
            Latitude      = clinic.Latitude,
            Longitude     = clinic.Longitude,
            BusinessHours = clinic.BusinessHours.Where(h => !h.IsDeleted)
                .OrderBy(h => h.DayOfWeek).ThenBy(h => h.StartTime).Select(MapHour).ToList(),
            ContactEmail  = string.IsNullOrWhiteSpace(clinic.ContactEmail) ? clinic.Email : clinic.ContactEmail,
            ContactPhone  = string.IsNullOrWhiteSpace(clinic.ContactPhone) ? clinic.Telefone : clinic.ContactPhone,
            LikeCount     = clinic.LikeCount,
            AcceptsAppointmentRequests = clinic.AcceptsAppointmentRequests,
        };

        return Result<PublicClinicDto>.Ok(dto);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private Task<List<ClinicBusinessHour>> LoadHours(int clinicaId)
        => db.ClinicBusinessHours
            .Where(h => h.ClinicaId == clinicaId && !h.IsDeleted)
            .OrderBy(h => h.DayOfWeek).ThenBy(h => h.StartTime)
            .ToListAsync();

    private static IReadOnlyList<ClinicCategoryDto> MapCategories(IEnumerable<ClinicCategory> categories)
        => categories.Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new ClinicCategoryDto { Id = c.Id, Name = c.Name, Slug = c.Slug })
            .ToList();

    private static BusinessHourDto MapHour(ClinicBusinessHour h) => new()
    {
        Id = h.Id, DayOfWeek = h.DayOfWeek, StartTime = h.StartTime, EndTime = h.EndTime
    };
}
