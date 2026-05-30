using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Appointment;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Interfaces;

public interface IAppointmentService
{
    Task<Result<PagedResult<AppointmentResponseDto>>> GetPagedAsync(
        AppointmentStatus? status,
        DateOnly? date,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? patientName,
        int page,
        int pageSize);

    Task<Result<AppointmentResponseDto>> GetByIdAsync(int id);

    Task<Result<AppointmentResponseDto>> CreateAsync(CreateAppointmentDto dto);

    Task<Result<AppointmentResponseDto>> UpdateAsync(int id, UpdateAppointmentDto dto);

    Task<Result<bool>> DeleteAsync(int id);
}
