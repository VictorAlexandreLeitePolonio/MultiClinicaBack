namespace MultiClinica.API.Models;

/// <summary>Categoria global de clínica (catálogo controlado). Vínculo N:N com Clinica.</summary>
public class ClinicCategory : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public ICollection<Clinica> Clinicas { get; set; } = [];
}

/// <summary>Faixa de horário de funcionamento. Mais de uma faixa por dia é permitida.</summary>
public class ClinicBusinessHour : AuditableEntity
{
    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

public enum ClinicMediaType
{
    Cover,
    Gallery
}

/// <summary>Mídia pública da clínica (capa/galeria), armazenada via IAttachmentStorage/S3.</summary>
public class ClinicMedia : AuditableEntity
{
    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;

    public string ObjectKey { get; set; } = string.Empty;
    public ClinicMediaType Type { get; set; }
    public int SortOrder { get; set; }
    public string? ContentType { get; set; }
    public long Size { get; set; }
}
