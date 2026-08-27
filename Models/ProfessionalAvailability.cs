namespace MultiClinica.API.Models;

public sealed class ProfessionalAvailability : AuditableEntity
{
    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
