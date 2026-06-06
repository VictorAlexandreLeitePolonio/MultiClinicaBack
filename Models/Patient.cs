namespace MultiClinica.API.Models;

public class Patient : AuditableEntity
{
    public int ClinicaId { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? CPF { get; set; }
    public string? Rg { get; set; }
    public string? Rua { get; set; }
    public string? Numero { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? Cep { get; set; }
    public string? Phone { get; set; }
    public Clinica Clinica { get; set; } = null!;
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<MedicalRecord> MedicalRecords { get; set; } = [];
    public ICollection<ClinicalAttachment> Attachments { get; set; } = [];
    public ICollection<PatientTreatment> Treatments { get; set; } = [];
}
