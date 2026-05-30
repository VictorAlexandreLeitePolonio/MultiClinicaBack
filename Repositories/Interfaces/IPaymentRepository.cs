using ProjetoLP.API.Models;

namespace ProjetoLP.API.Repositories.Interfaces;

public interface IPaymentRepository
{
    /// <summary>Retorna pagamentos paginados com filtros opcionais.</summary>
    Task<(List<Payment> Items, int TotalCount)> GetPagedAsync(
        int? patientId,
        PaymentStatus? status,
        string? referenceMonth,
        string? patientName,
        int page,
        int pageSize);

    /// <summary>Busca um pagamento pelo Id, incluindo Patient e Plan.</summary>
    Task<Payment?> GetByIdAsync(int id);

    /// <summary>Verifica se já existe pagamento para o paciente naquele mês.</summary>
    Task<bool> ExistsAsync(int patientId, string referenceMonth);

    /// <summary>Adiciona e salva um novo pagamento.</summary>
    Task<Payment> AddAsync(Payment payment);

    /// <summary>Salva alterações em um pagamento já rastreado pelo EF Core.</summary>
    Task SaveChangesAsync();

    /// <summary>Remove um pagamento já rastreado.</summary>
    Task DeleteAsync(Payment payment);
}
