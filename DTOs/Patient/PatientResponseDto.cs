using ProjetoLP.API.Models;

namespace ProjetoLP.API.DTOs.Patient
{
    public class PatientResponseDto
    {
        public int Id { get; set; }
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
        public bool IsActive { get; set; }
        public AppointmentStatus appointmentStatus { get; set; }
        public PaymentStatus paymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
       
    }
}
