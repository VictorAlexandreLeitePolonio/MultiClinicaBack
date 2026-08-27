using MultiClinica.API.DTOs.Clinic;

namespace MultiClinica.API.DTOs.Marketplace;

public enum MarketplaceClinicSort
{
    MostLiked,
    NameAsc,
    NameDesc,
}

public sealed class MarketplaceClinicQuery
{
    public string? Search { get; set; }
    public int[] CategoryIds { get; set; } = [];
    public string? City { get; set; }
    public string? State { get; set; }
    public bool? AcceptsAppointmentRequests { get; set; }
    public bool? LikedOnly { get; set; }
    public MarketplaceClinicSort Sort { get; set; } = MarketplaceClinicSort.MostLiked;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public sealed class MarketplaceClinicCardDto
{
    public int Id { get; set; }
    public string? Slug { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? CoverUrl { get; set; }
    public IReadOnlyList<ClinicCategoryDto> Categories { get; set; } = [];
    public string? City { get; set; }
    public string? State { get; set; }
    public int LikeCount { get; set; }
    public bool LikedByMe { get; set; }
    public bool AcceptsAppointmentRequests { get; set; }
}

public sealed class MarketplaceClinicDetailsDto
{
    public int Id { get; set; }
    public string? Slug { get; set; }
    public string DisplayName { get; set; } = string.Empty;
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
    public bool LikedByMe { get; set; }
    public bool AcceptsAppointmentRequests { get; set; }
    public bool IsLinked { get; set; }
}
