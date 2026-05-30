namespace ProjetoLP.API.DTOs.Appointment;

// Dados recebidos na criação de uma consulta.
// Status não está aqui — toda consulta começa como Scheduled automaticamente.
public class CreateAppointmentDto
{
    public int UserId { get; set; }
    public int PatientId { get; set; }
    public DateTime AppointmentDate { get; set; }
}
