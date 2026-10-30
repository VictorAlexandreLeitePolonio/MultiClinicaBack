using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.SuperAdmin;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/superadmin")]
public class SuperAdminHubController(
    AppDbContext db,
    ClinicPlanService plans,
    IUsuarioLogadoService usuario) : ControllerBase
{
    [HttpGet("hub-approvals")]
    public async Task<IActionResult> GetApprovals()
    {
        var pending = await db.Clinicas
            .AsNoTracking()
            .Where(clinic => clinic.IsPublic && !clinic.IsHubApproved && !clinic.IsDeleted)
            .OrderBy(clinic => clinic.UpdatedAt)
            .Select(clinic => new
            {
                clinicaId = clinic.Id,
                name = string.IsNullOrWhiteSpace(clinic.DisplayName)
                    ? (string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia)
                    : clinic.DisplayName,
                publicSlug = clinic.PublicSlug,
                city = clinic.Cidade,
                updatedAt = clinic.UpdatedAt,
            })
            .ToListAsync();
        return Ok(pending);
    }

    [HttpPost("clinicas/{id:int}/hub-approval")]
    public async Task<IActionResult> SetApproval(int id, HubApprovalDto dto)
    {
        var clinic = await db.Clinicas.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);
        if (clinic is null)
            return NotFound(new { message = "Clínica não encontrada." });

        if (dto.Approved)
        {
            clinic.IsHubApproved = true;
            clinic.HubApprovedAt = DateTime.UtcNow;
            AddHistory(clinic.Id, CommercialHistoryEventType.HubApproved, "Clínica aprovada no hub.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(new { message = "Motivo da reprovação é obrigatório." });
            clinic.IsHubApproved = false;
            clinic.IsPublic = false;
            AddHistory(clinic.Id, CommercialHistoryEventType.HubRejected, dto.Reason.Trim());
        }

        clinic.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("clinicas/{id:int}/plan")]
    public async Task<IActionResult> SetPlan(int id, SetClinicPlanDto dto)
    {
        var clinic = await db.Clinicas.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted);
        if (clinic is null)
            return NotFound(new { message = "Clínica não encontrada." });

        clinic.IsFounder = dto.IsFounder;
        await plans.ApplyPlanAsync(clinic, dto.IsFounder ? ClinicPlan.Premium : dto.Plan, null);
        clinic.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private void AddHistory(int clinicaId, CommercialHistoryEventType type, string description) =>
        db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
        {
            ClinicaId = clinicaId,
            Type = type,
            Description = description,
            CreatedByUserId = usuario.UserId,
        });
}
