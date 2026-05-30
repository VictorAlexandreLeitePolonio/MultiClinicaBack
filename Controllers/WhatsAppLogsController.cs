using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.WhatsAppLog;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class WhatsAppLogsController : ControllerBase
{
    private readonly AppDbContext _db;

    public WhatsAppLogsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/whatsapplogs
    // Filtros: ?type=AppointmentReminder|PaymentReminder &success=true|false &patientId=X
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? type,
        [FromQuery] bool?   success,
        [FromQuery] int?    patientId,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        var query = _db.WhatsAppLogs
            .Include(w => w.Patient)
            .AsQueryable();

        if (!string.IsNullOrEmpty(type))
            query = query.Where(w => w.Type == type);

        if (success.HasValue)
            query = query.Where(w => w.Success == success.Value);

        if (patientId.HasValue)
            query = query.Where(w => w.PatientId == patientId.Value);

        var totalCount = await query.CountAsync();

        var logs = await query
            .OrderByDescending(w => w.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = logs.Select(w => new WhatsAppLogResponseDto
        {
            Id           = w.Id,
            PatientId    = w.PatientId,
            PatientName  = w.Patient?.Name,
            Phone        = w.Phone,
            Message      = w.Message,
            Type         = w.Type,
            Success      = w.Success,
            ErrorMessage = w.ErrorMessage,
            SentAt       = w.SentAt,
        });

        return Ok(new PagedResult<WhatsAppLogResponseDto>
        {
            Data       = data,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        });
    }
}
