namespace MultiClinica.API.DTOs.Appointment;

using MultiClinica.API.Models;

// Dados retornados ao cliente nas respostas da API.
public class AppointmentResponseDto
{
    public int Id { get; set; }
    public int ProfessionalId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? TimeZoneId { get; set; }
    // Recording | Processing | Ready | Interrupted | Failed; null sem consulta gravada.
    public string? ConsultationStatus { get; set; }
    public bool ConsultationReviewed { get; set; }
}
