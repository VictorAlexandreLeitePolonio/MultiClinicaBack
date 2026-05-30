using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.SuperAdmin;

public class CreateClinicaDto
{
    public string Nome { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string NomeResponsavel { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Rua { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
    public CreateFirstAdminDto FirstAdmin { get; set; } = new();
}

public class CreateFirstAdminDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class UpdateClinicaDto
{
    public string Nome { get; set; } = string.Empty;
    public string NomeFantasia { get; set; } = string.Empty;
    public string NomeResponsavel { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Rua { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Cep { get; set; } = string.Empty;
}

public class UpdateClinicaStatusDto
{
    public bool IsActive { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class UpdateBillingConfigDto
{
    public decimal ValorMensalidade { get; set; }
    public int DiaVencimento { get; set; }
    public bool CobrancaAtiva { get; set; }
    public DateOnly? DataInicioCobranca { get; set; }
}

public class RegisterClinicPaymentDto
{
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class CancelClinicChargeDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ManualUnblockDto
{
    public string Reason { get; set; } = string.Empty;
}

public class SuperAdminCreateUserDto
{
    public int ClinicaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Profissional;
}
