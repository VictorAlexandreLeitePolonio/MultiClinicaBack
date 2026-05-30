namespace MultiClinica.API.Models;

public enum UserRole
{
    SuperAdmin,
    Administrador,
    Profissional,
    Recepcao
}

public class User : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CPF { get; set; } = string.Empty;
    public string Rg { get; set; } = string.Empty;
    public string Rua { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Profissional;

    public Clinica Clinica { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
}
