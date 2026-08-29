using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Clinic;

// ── Perfil público ───────────────────────────────────────────────────────────

public sealed class PublicClinicDto
{
    public int Id { get; set; }
    public string? Slug { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
    public IReadOnlyList<string> Gallery { get; set; } = [];
    public IReadOnlyList<ClinicCategoryDto> Categories { get; set; } = [];
    public ClinicAddressDto Address { get; set; } = new();
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public IReadOnlyList<BusinessHourDto> BusinessHours { get; set; } = [];
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int LikeCount { get; set; }
    public bool AcceptsAppointmentRequests { get; set; }
}

/// <summary>Resumo para a vitrine pública (landing page) — sem dados de contato.</summary>
public sealed class PublicClinicSummaryDto
{
    public int Id { get; set; }
    public string? Slug { get; set; }
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public IReadOnlyList<ClinicCategoryDto> Categories { get; set; } = [];
    public int LikeCount { get; set; }
    public bool AcceptsAppointmentRequests { get; set; }
}

// ── Categorias ───────────────────────────────────────────────────────────────

public sealed class ClinicCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class SetClinicCategoriesRequest
{
    public List<int> CategoryIds { get; set; } = [];
}

// ── Horários ─────────────────────────────────────────────────────────────────

public sealed class BusinessHourDto
{
    public int Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public sealed class CreateBusinessHourRequest
{
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

// ── Mídia ────────────────────────────────────────────────────────────────────

public sealed class ClinicMediaDto
{
    public int Id { get; set; }
    public ClinicMediaType Type { get; set; }
    public int SortOrder { get; set; }
    public string Url { get; set; } = string.Empty;
}

public sealed class UpdateMediaOrderRequest
{
    public int SortOrder { get; set; }
}
