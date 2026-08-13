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
}
