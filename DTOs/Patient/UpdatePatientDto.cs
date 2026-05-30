namespace MultiClinica.API.DTOs.Patient
{
    public class UpdatePatientDto
    {
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
    }
}
