namespace MultiClinica.API.Models;

/// <summary>
/// Like de uma clínica por um paciente. Unicidade garantida por índice
/// (PatientAccountId, ClinicaId). O total é materializado em
/// <see cref="Clinica.LikeCount"/> para leitura rápida.
/// </summary>
public class ClinicLike
{
    public int Id { get; set; }
    public int PatientAccountId { get; set; }
    public PatientAccount PatientAccount { get; set; } = null!;
    public int ClinicaId { get; set; }
    public Clinica Clinica { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
