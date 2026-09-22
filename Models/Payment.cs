// Define o namespace — organiza o arquivo dentro da pasta Models.
namespace MultiClinica.API.Models;

// Enum com os estados possíveis de um pagamento.
public enum PaymentStatus
{
    Pending,   // Pagamento pendente (estado inicial)
    Paid,      // Pagamento confirmado
    Cancelled  // Pagamento cancelado
}

// Classe que representa a tabela "Payments" (Financeiro) no banco.
public class Payment : AuditableEntity
{
    public int ClinicaId { get; set; }
    // Chave estrangeira — liga o pagamento ao usuário/paciente responsável.
    public int PatientId { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }

    public DateOnly ReferenceMonth { get; set; }

    // Valor do pagamento. "decimal" é o tipo correto para dinheiro —
    // evita erros de arredondamento que ocorrem com float/double.
    public decimal Amount { get; set; }

    // Forma de pagamento (ex: "Pix", "Cartão", "Dinheiro").
    public string PaymentMethod { get; set; } = string.Empty;

    // Estado do pagamento — inicia como Pending por padrão.
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    // Data em que o pagamento foi confirmado — nullable porque só é preenchido quando pago.
    // Não tem valor padrão pois começa nulo (ainda não foi pago).
    public DateOnly? PaidAt { get; set; }

    // Data de vencimento do pagamento — usada pelo PaymentReminderJob para enviar lembrete 24h antes.
    public DateOnly? PaymentDate { get; set; }

    public Clinica Clinica { get; set; } = null!;
    public User User { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public Plans Plan { get; set; } = null!;
}
