// Define o namespace — organiza o arquivo dentro da pasta Models.
namespace ProjetoLP.API.Models;

// Enum com os estados possíveis de um pagamento.
public enum PaymentStatus
{
    Pending,   // Pagamento pendente (estado inicial)
    Paid,      // Pagamento confirmado
    Cancelled  // Pagamento cancelado
}

// Classe que representa a tabela "Payments" (Financeiro) no banco.
public class Payment
{
    // Chave primária com autoincrement.
    public int Id { get; set; }

    // Chave estrangeira — liga o pagamento ao usuário/paciente responsável.
    public int PatientId { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }

    // Mês de referência do pagamento. Formato: "2026-03".
    // Usando string para simplicidade — identifica qual mês está sendo cobrado.
    public string ReferenceMonth { get; set; } = string.Empty;

    // Valor do pagamento. "decimal" é o tipo correto para dinheiro —
    // evita erros de arredondamento que ocorrem com float/double.
    public decimal Amount { get; set; }

    // Forma de pagamento (ex: "Pix", "Cartão", "Dinheiro").
    public string PaymentMethod { get; set; } = string.Empty;

    // Estado do pagamento — inicia como Pending por padrão.
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    // Data em que o pagamento foi confirmado — nullable porque só é preenchido quando pago.
    // Não tem valor padrão pois começa nulo (ainda não foi pago).
    public DateTime? PaidAt { get; set; }

    // Data de vencimento do pagamento — usada pelo PaymentReminderJob para enviar lembrete 24h antes.
    public DateTime? PaymentDate { get; set; }

    // Controla se o lembrete de vencimento já foi enviado via WhatsApp — evita envios duplicados.
    public bool PaymentReminderSent { get; set; } = false;

    // Preenchido automaticamente com a data/hora de criação do registro.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public Plans Plan { get; set; } = null!;
}
