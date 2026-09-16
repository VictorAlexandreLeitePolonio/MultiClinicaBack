namespace MultiClinica.API.Models;

public class SessionType
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ClinicaId { get; set; }

    public Clinica Clinica { get; set; } = null!;
}
