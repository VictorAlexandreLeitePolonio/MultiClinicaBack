namespace ProjetoLP.API.DTOs.WhatsAppLog;

public class WhatsAppLogResponseDto
{
    public int      Id           { get; set; }
    public int?     PatientId    { get; set; }
    public string?  PatientName  { get; set; }
    public string   Phone        { get; set; } = string.Empty;
    public string   Message      { get; set; } = string.Empty;
    public string   Type         { get; set; } = string.Empty;
    public bool     Success      { get; set; }
    public string?  ErrorMessage { get; set; }
    public DateTime SentAt       { get; set; }
}
