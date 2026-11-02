using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.Payment;

// Quadro de pagamentos: contagem e soma por status sobre todos os pagamentos do filtro, não só a página.
public sealed record PaymentStatusSummaryDto(PaymentStatus Status, int Count, decimal Total);
