namespace MultiClinica.API.DTOs.Patient
{
    using System.Text.Json.Serialization;

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
        private DateOnly? _birthDate;

        public DateOnly? BirthDate
        {
            get => _birthDate;
            set
            {
                _birthDate = value;
                BirthDateProvided = true;
            }
        }

        [JsonIgnore]
        public bool BirthDateProvided { get; private set; }
    }
}
