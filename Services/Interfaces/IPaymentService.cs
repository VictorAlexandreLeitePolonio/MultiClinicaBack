using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Payment;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

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
