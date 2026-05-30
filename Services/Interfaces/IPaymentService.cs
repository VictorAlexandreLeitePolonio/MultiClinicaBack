using ProjetoLP.API.Common;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Payment;
using ProjetoLP.API.Models;

namespace ProjetoLP.API.Services.Interfaces;

public interface IPaymentService
{
    Task<Result<PagedResult<PaymentResponseDto>>> GetPagedAsync(
        int? patientId,
        PaymentStatus? status,
        string? referenceMonth,
        string? patientName,
        int page,
        int pageSize);

    Task<Result<PaymentResponseDto>> GetByIdAsync(int id);

    Task<Result<PaymentResponseDto>> CreateAsync(CreatePaymentDto dto);

    Task<Result<PaymentResponseDto>> UpdateAsync(int id, UpdatePaymentDto dto);

    Task<Result<bool>> DeleteAsync(int id);
}
