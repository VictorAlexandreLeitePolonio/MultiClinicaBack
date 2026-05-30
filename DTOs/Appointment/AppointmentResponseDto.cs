namespace ProjetoLP.API.DTOs.Appointment;

using ProjetoLP.API.Models;

// Dados retornados ao cliente nas respostas da API.
public class AppointmentResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
