namespace MultiClinica.API.DTOs.Patient;

public class PatientImportResponseDto
{
    public Guid ImportId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int RejectedCount { get; set; }
    public int EmailsQueuedCount { get; set; }
    public int EmailsSkippedNoEmailCount { get; set; }
    public List<PatientImportRowResultDto> Results { get; set; } = [];
}

public class PatientImportRowResultDto
{
    public int Row { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? PatientId { get; set; }
    public string? EmailStatus { get; set; }
    public List<PatientImportRowErrorDto> Errors { get; set; } = [];
}

public class PatientImportRowErrorDto
{
    public string Field { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class PatientImportErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
