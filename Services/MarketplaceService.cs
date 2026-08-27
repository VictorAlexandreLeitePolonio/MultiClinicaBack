using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.DTOs.Marketplace;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public sealed class MarketplaceService(
    AppDbContext db,
    IPatientAccountLoggedService patient,
    IAttachmentStorage storage) : IMarketplaceService
{
    private static readonly TimeSpan MediaUrlTtl = TimeSpan.FromHours(1);

    public async Task<Result<IReadOnlyList<ClinicCategoryDto>>> GetCategoriesAsync()
    {
        var categories = await db.ClinicCategories
            .AsNoTracking()
            .Where(category => category.IsActive && !category.IsDeleted)
            .OrderBy(category => category.Name)
            .Select(category => new ClinicCategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
            })
            .ToListAsync();

        return Result<IReadOnlyList<ClinicCategoryDto>>.Ok(categories);
    }

    public async Task<Result<PagedResult<MarketplaceClinicCardDto>>> GetClinicsAsync(
        MarketplaceClinicQuery request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var query = db.Clinicas
            .AsNoTracking()
            .Where(clinic => clinic.IsPublic && clinic.IsActive && !clinic.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(clinic =>
                clinic.DisplayName != null && clinic.DisplayName.ToLower().Contains(search)
                || clinic.NomeFantasia.ToLower().Contains(search)
                || clinic.Nome.ToLower().Contains(search));
        }

        var categoryIds = request.CategoryIds.Distinct().ToArray();
        if (categoryIds.Length > 0)
        {
            query = query.Where(clinic => clinic.Categories.Any(category =>
                categoryIds.Contains(category.Id) && category.IsActive && !category.IsDeleted));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City.Trim().ToLower();
            query = query.Where(clinic => clinic.Cidade.ToLower() == city);
        }

        if (!string.IsNullOrWhiteSpace(request.State))
        {
            var state = request.State.Trim().ToLower();
            query = query.Where(clinic => clinic.Estado.ToLower() == state);
        }

        if (request.AcceptsAppointmentRequests.HasValue)
        {
            query = query.Where(clinic =>
                clinic.AcceptsAppointmentRequests == request.AcceptsAppointmentRequests.Value);
        }

        if (request.LikedOnly == true)
        {
            query = query.Where(clinic => db.ClinicLikes.Any(like =>
                like.ClinicaId == clinic.Id && like.PatientAccountId == patient.PatientAccountId));
        }

        var totalCount = await query.CountAsync();
        query = request.Sort switch
        {
            MarketplaceClinicSort.NameAsc => query
                .OrderBy(clinic => string.IsNullOrWhiteSpace(clinic.DisplayName)
                    ? (string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia)
                    : clinic.DisplayName)
                .ThenBy(clinic => clinic.Id),
            MarketplaceClinicSort.NameDesc => query
                .OrderByDescending(clinic => string.IsNullOrWhiteSpace(clinic.DisplayName)
                    ? (string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia)
                    : clinic.DisplayName)
                .ThenBy(clinic => clinic.Id),
            _ => query.OrderByDescending(clinic => clinic.LikeCount).ThenBy(clinic => clinic.Id),
        };

        var clinics = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(clinic => clinic.Categories)
            .ToListAsync();

        var clinicIds = clinics.Select(clinic => clinic.Id).ToArray();
        var covers = await db.ClinicMedia
            .AsNoTracking()
            .Where(media => clinicIds.Contains(media.ClinicaId)
                && media.Type == ClinicMediaType.Cover
                && !media.IsDeleted)
            .OrderBy(media => media.SortOrder)
            .ToListAsync();
        var coverKeys = covers
            .GroupBy(media => media.ClinicaId)
            .ToDictionary(group => group.Key, group => group.First().ObjectKey);
        var likedClinicIds = (await db.ClinicLikes
            .AsNoTracking()
            .Where(like => like.PatientAccountId == patient.PatientAccountId
                && clinicIds.Contains(like.ClinicaId))
            .Select(like => like.ClinicaId)
            .ToListAsync())
            .ToHashSet();

        var cards = new List<MarketplaceClinicCardDto>(clinics.Count);
        foreach (var clinic in clinics)
        {
            string? coverUrl = null;
            if (coverKeys.TryGetValue(clinic.Id, out var coverKey))
                coverUrl = await storage.CreateReadUrlAsync(coverKey, MediaUrlTtl);

            cards.Add(new MarketplaceClinicCardDto
            {
                Id = clinic.Id,
                Slug = clinic.PublicSlug,
                DisplayName = ResolveDisplayName(clinic),
                LogoUrl = clinic.LogoUrl,
                CoverUrl = coverUrl,
                Categories = MapCategories(clinic.Categories),
                City = EmptyToNull(clinic.Cidade),
                State = EmptyToNull(clinic.Estado),
                LikeCount = clinic.LikeCount,
                LikedByMe = likedClinicIds.Contains(clinic.Id),
                AcceptsAppointmentRequests = clinic.AcceptsAppointmentRequests,
            });
        }

        return Result<PagedResult<MarketplaceClinicCardDto>>.Ok(new PagedResult<MarketplaceClinicCardDto>
        {
            Data = cards,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        });
    }

    public async Task<Result<MarketplaceClinicDetailsDto>> GetClinicAsync(int clinicId)
    {
        var clinic = await db.Clinicas
            .AsNoTracking()
            .Include(item => item.Categories)
            .Include(item => item.BusinessHours)
            .Include(item => item.Media)
            .FirstOrDefaultAsync(item => item.Id == clinicId
                && item.IsPublic && item.IsActive && !item.IsDeleted);

        if (clinic is null)
            return Result<MarketplaceClinicDetailsDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        var media = clinic.Media.Where(item => !item.IsDeleted).OrderBy(item => item.SortOrder).ToList();
        var cover = media.FirstOrDefault(item => item.Type == ClinicMediaType.Cover);
        var gallery = new List<string>();
        foreach (var item in media.Where(item => item.Type == ClinicMediaType.Gallery))
            gallery.Add(await storage.CreateReadUrlAsync(item.ObjectKey, MediaUrlTtl));

        var likedByMe = await db.ClinicLikes.AsNoTracking().AnyAsync(like =>
            like.PatientAccountId == patient.PatientAccountId && like.ClinicaId == clinicId);
        var isLinked = await db.Patients.AsNoTracking().AnyAsync(link =>
            link.PatientAccountId == patient.PatientAccountId
            && link.ClinicaId == clinicId
            && !link.IsDeleted);

        return Result<MarketplaceClinicDetailsDto>.Ok(new MarketplaceClinicDetailsDto
        {
            Id = clinic.Id,
            Slug = clinic.PublicSlug,
            DisplayName = ResolveDisplayName(clinic),
            Description = clinic.Description,
            LogoUrl = clinic.LogoUrl,
            CoverUrl = cover is null ? null : await storage.CreateReadUrlAsync(cover.ObjectKey, MediaUrlTtl),
            Gallery = gallery,
            Categories = MapCategories(clinic.Categories),
            Address = new ClinicAddressDto
            {
                Rua = EmptyToNull(clinic.Rua),
                Numero = EmptyToNull(clinic.Numero),
                Bairro = EmptyToNull(clinic.Bairro),
                Cidade = EmptyToNull(clinic.Cidade),
                Estado = EmptyToNull(clinic.Estado),
                Cep = EmptyToNull(clinic.Cep),
            },
            Latitude = clinic.Latitude,
            Longitude = clinic.Longitude,
            BusinessHours = clinic.BusinessHours
                .Where(hour => !hour.IsDeleted)
                .OrderBy(hour => hour.DayOfWeek)
                .ThenBy(hour => hour.StartTime)
                .Select(MapHour)
                .ToList(),
            ContactEmail = EmptyToNull(clinic.ContactEmail) ?? EmptyToNull(clinic.Email),
            ContactPhone = EmptyToNull(clinic.ContactPhone) ?? EmptyToNull(clinic.Telefone),
            LikeCount = clinic.LikeCount,
            LikedByMe = likedByMe,
            AcceptsAppointmentRequests = clinic.AcceptsAppointmentRequests,
            IsLinked = isLinked,
        });
    }

    private static string ResolveDisplayName(Clinica clinic)
        => !string.IsNullOrWhiteSpace(clinic.DisplayName)
            ? clinic.DisplayName
            : !string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.NomeFantasia : clinic.Nome;

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<ClinicCategoryDto> MapCategories(IEnumerable<ClinicCategory> categories)
        => categories
            .Where(category => category.IsActive && !category.IsDeleted)
            .OrderBy(category => category.Name)
            .Select(category => new ClinicCategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
            })
            .ToList();

    private static BusinessHourDto MapHour(ClinicBusinessHour hour) => new()
    {
        Id = hour.Id,
        DayOfWeek = hour.DayOfWeek,
        StartTime = hour.StartTime,
        EndTime = hour.EndTime,
    };
}
