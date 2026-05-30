namespace ProjetoLP.API.DTOs.Appointment;

using ProjetoLP.API.Models;

// Dados permitidos na atualização de uma consulta.
// UserId não está aqui — o dono da consulta nunca muda.
public class UpdateAppointmentDto
{
    public DateTime AppointmentDate { get; set; }
    public AppointmentStatus Status { get; set; }
}
