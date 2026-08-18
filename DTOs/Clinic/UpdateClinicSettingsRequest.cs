namespace MultiClinica.API.DTOs.Clinic;

public sealed class UpdateClinicSettingsRequest
{
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    // ── Presença pública (BACK-5) ────────────────────────────────────────────
    public string? PublicSlug { get; set; }
    public string? Description { get; set; }
    public bool? IsPublic { get; set; }
    public bool? AcceptsAppointmentRequests { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public ClinicAddressDto? Address { get; set; }
}
