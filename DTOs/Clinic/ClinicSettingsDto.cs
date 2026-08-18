namespace MultiClinica.API.DTOs.Clinic;

public sealed class ClinicSettingsDto
{
    public int ClinicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? AccentColor { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    // ── Presença pública (BACK-5) ────────────────────────────────────────────
    public string? PublicSlug { get; set; }
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
    public bool AcceptsAppointmentRequests { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Endereço reaproveitado da própria Clinica (sem duplicar dados).
    public ClinicAddressDto Address { get; set; } = new();
    public int LikeCount { get; set; }
}

public sealed class ClinicAddressDto
{
    public string? Rua { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Cep { get; set; }
}
