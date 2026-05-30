using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.SuperAdmin;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "SuperAdmin")]
[ApiController]
[Route("api/superadmin/clinicas")]
public class SuperAdminClinicasController(AppDbContext db, IUsuarioLogadoService usuario) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetClinicas(
        [FromQuery] bool? isActive,
        [FromQuery] bool? cobrancaAtiva,
        [FromQuery] bool? isBlockedByBilling,
        [FromQuery] bool? overdue)
    {
        var query = db.Clinicas.Where(c => !c.IsDeleted).AsQueryable();

        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);
        if (cobrancaAtiva.HasValue)
            query = query.Where(c => c.CobrancaAtiva == cobrancaAtiva.Value);
        if (isBlockedByBilling.HasValue)
            query = query.Where(c => c.IsBlockedByBilling == isBlockedByBilling.Value);
        if (overdue.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = overdue.Value
                ? query.Where(c => c.Charges.Any(ch => ch.Status == ClinicChargeStatus.Pending && ch.DueDate < today))
                : query.Where(c => !c.Charges.Any(ch => ch.Status == ClinicChargeStatus.Pending && ch.DueDate < today));
        }

        var clinicas = await query
            .OrderBy(c => c.Nome)
            .Select(c => ToClinicaResponse(c))
            .ToListAsync();

        return Ok(clinicas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetClinica(int id)
    {
        var clinica = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        return clinica is null ? NotFound(new { message = "Clínica não encontrada." }) : Ok(ToClinicaResponse(clinica));
    }

    [HttpPost]
    public async Task<IActionResult> CreateClinica(CreateClinicaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            return BadRequest(new { message = "Razão social é obrigatória." });
        if (dto.FirstAdmin.Password.Length < 6)
            return BadRequest(new { message = "A senha do primeiro administrador deve ter no mínimo 6 caracteres." });

        var adminEmail = NormalizeEmail(dto.FirstAdmin.Email);
        if (await db.Users.AnyAsync(u => u.Email == adminEmail && !u.IsDeleted))
            return Conflict(new { message = "Email já cadastrado por outro usuário." });

        var clinica = new Clinica();
        ApplyClinicaFields(clinica, dto);
        clinica.IsActive = true;
        clinica.CobrancaAtiva = false;
        clinica.CreatedByUserId = usuario.UserId;
        db.Clinicas.Add(clinica);
        await db.SaveChangesAsync();

        var admin = new User
        {
            ClinicaId = clinica.Id,
            Name = dto.FirstAdmin.Name.Trim(),
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.FirstAdmin.Password),
            Role = UserRole.Administrador,
            CreatedByUserId = usuario.UserId
        };

        db.Users.Add(admin);
        AddHistory(clinica.Id, CommercialHistoryEventType.ClinicCreated, "Clínica criada.");
        AddHistory(clinica.Id, CommercialHistoryEventType.AdminUserCreated, $"Primeiro administrador criado: {admin.Email}.");
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetClinica), new { id = clinica.Id }, ToClinicaResponse(clinica));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateClinica(int id, UpdateClinicaDto dto)
    {
        var clinica = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (clinica is null)
            return NotFound(new { message = "Clínica não encontrada." });

        ApplyClinicaFields(clinica, dto);
        clinica.UpdatedByUserId = usuario.UserId;
        await db.SaveChangesAsync();
        return Ok(ToClinicaResponse(clinica));
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, UpdateClinicaStatusDto dto)
    {
        var clinica = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (clinica is null)
            return NotFound(new { message = "Clínica não encontrada." });

        clinica.IsActive = dto.IsActive;
        clinica.UpdatedByUserId = usuario.UserId;
        AddHistory(
            clinica.Id,
            dto.IsActive ? CommercialHistoryEventType.ClinicActivated : CommercialHistoryEventType.ClinicDeactivated,
            string.IsNullOrWhiteSpace(dto.Reason) ? (dto.IsActive ? "Clínica ativada." : "Clínica desativada.") : dto.Reason);

        await db.SaveChangesAsync();
        return Ok(ToClinicaResponse(clinica));
    }

    [HttpPut("{id:int}/billing")]
    public async Task<IActionResult> UpdateBilling(int id, UpdateBillingConfigDto dto)
    {
        var clinica = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (clinica is null)
            return NotFound(new { message = "Clínica não encontrada." });
        if (dto.ValorMensalidade < 0 || dto.DiaVencimento is < 1 or > 28)
            return BadRequest(new { message = "Configuração de cobrança inválida." });

        clinica.ValorMensalidade = dto.ValorMensalidade;
        clinica.DiaVencimento = dto.DiaVencimento;
        clinica.CobrancaAtiva = dto.CobrancaAtiva;
        clinica.DataInicioCobranca = dto.DataInicioCobranca;
        clinica.UpdatedByUserId = usuario.UserId;
        AddHistory(clinica.Id, CommercialHistoryEventType.BillingConfigChanged, "Configuração de cobrança alterada.");
        await db.SaveChangesAsync();
        return Ok(ToClinicaResponse(clinica));
    }

    [HttpGet("{id:int}/charges")]
    public async Task<IActionResult> GetCharges(int id)
    {
        if (!await db.Clinicas.AnyAsync(c => c.Id == id && !c.IsDeleted))
            return NotFound(new { message = "Clínica não encontrada." });

        var charges = await db.ClinicCharges
            .Where(c => c.ClinicaId == id && !c.IsDeleted)
            .OrderByDescending(c => c.ReferenceMonth)
            .ToListAsync();
        return Ok(charges);
    }

    [HttpPost("{id:int}/charges/{chargeId:int}/payments")]
    public async Task<IActionResult> RegisterPayment(int id, int chargeId, RegisterClinicPaymentDto dto)
    {
        var charge = await db.ClinicCharges.FirstOrDefaultAsync(c => c.Id == chargeId && c.ClinicaId == id && !c.IsDeleted);
        if (charge is null)
            return NotFound(new { message = "Cobrança não encontrada." });

        charge.Status = ClinicChargeStatus.Paid;
        charge.PaidAt = dto.PaidAt ?? DateTime.UtcNow;
        charge.PaymentMethod = dto.PaymentMethod.Trim();
        charge.Notes = dto.Notes.Trim();
        charge.UpdatedByUserId = usuario.UserId;

        var clinica = await db.Clinicas.FindAsync(id);
        if (clinica is not null)
        {
            clinica.IsBlockedByBilling = false;
            clinica.BillingBlockedAt = null;
            clinica.BillingBlockReason = null;
        }

        AddHistory(id, CommercialHistoryEventType.PaymentRegistered, $"Pagamento registrado para {charge.ReferenceMonth}.");
        await db.SaveChangesAsync();
        return Ok(charge);
    }

    [HttpPost("{id:int}/charges/{chargeId:int}/cancel")]
    public async Task<IActionResult> CancelCharge(int id, int chargeId, CancelClinicChargeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Motivo do cancelamento é obrigatório." });

        var charge = await db.ClinicCharges.FirstOrDefaultAsync(c => c.Id == chargeId && c.ClinicaId == id && !c.IsDeleted);
        if (charge is null)
            return NotFound(new { message = "Cobrança não encontrada." });

        charge.Status = ClinicChargeStatus.Cancelled;
        charge.Notes = dto.Reason.Trim();
        charge.UpdatedByUserId = usuario.UserId;
        AddHistory(id, CommercialHistoryEventType.PaymentCancelled, dto.Reason.Trim());
        await db.SaveChangesAsync();
        return Ok(charge);
    }

    [HttpPost("{id:int}/billing/unblock")]
    public async Task<IActionResult> ManualUnblock(int id, ManualUnblockDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Motivo do desbloqueio é obrigatório." });

        var clinica = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (clinica is null)
            return NotFound(new { message = "Clínica não encontrada." });

        clinica.IsBlockedByBilling = false;
        clinica.BillingBlockedAt = null;
        clinica.BillingBlockReason = null;
        clinica.UpdatedByUserId = usuario.UserId;
        AddHistory(id, CommercialHistoryEventType.ManualBillingUnblock, dto.Reason.Trim());
        await db.SaveChangesAsync();
        return Ok(ToClinicaResponse(clinica));
    }

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id)
    {
        if (!await db.Clinicas.AnyAsync(c => c.Id == id && !c.IsDeleted))
            return NotFound(new { message = "Clínica não encontrada." });

        var history = await db.CommercialHistoryEvents
            .Where(h => h.ClinicaId == id && !h.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
        return Ok(history);
    }

    [HttpGet("{id:int}/users")]
    public async Task<IActionResult> GetUsers(int id)
    {
        if (!await db.Clinicas.AnyAsync(c => c.Id == id && !c.IsDeleted))
            return NotFound(new { message = "Clínica não encontrada." });

        var users = await db.Users
            .Where(u => u.ClinicaId == id && !u.IsDeleted)
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, Role = u.Role.ToString(), u.IsActive })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser(SuperAdminCreateUserDto dto)
    {
        if (!await db.Clinicas.AnyAsync(c => c.Id == dto.ClinicaId && !c.IsDeleted))
            return NotFound(new { message = "Clínica não encontrada." });
        if (dto.Password.Length < 6)
            return BadRequest(new { message = "A senha deve ter no mínimo 6 caracteres." });

        var email = NormalizeEmail(dto.Email);
        if (await db.Users.AnyAsync(u => u.Email == email && !u.IsDeleted))
            return Conflict(new { message = "Email já cadastrado por outro usuário." });

        var user = new User
        {
            ClinicaId = dto.ClinicaId,
            Name = dto.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            CreatedByUserId = usuario.UserId
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Created($"/api/superadmin/clinicas/{dto.ClinicaId}/users/{user.Id}", new
        {
            user.Id,
            user.Name,
            user.Email,
            Role = user.Role.ToString(),
            user.ClinicaId
        });
    }

    private void AddHistory(int clinicaId, CommercialHistoryEventType type, string description)
    {
        db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
        {
            ClinicaId = clinicaId,
            Type = type,
            Description = description,
            CreatedByUserId = usuario.UserId
        });
    }

    private static object ToClinicaResponse(Clinica c) => new
    {
        c.Id,
        c.Nome,
        c.NomeFantasia,
        c.NomeResponsavel,
        c.Cnpj,
        c.Email,
        c.Telefone,
        c.Rua,
        c.Numero,
        c.Bairro,
        c.Cidade,
        c.Estado,
        c.Cep,
        c.IsActive,
        c.IsBlockedByBilling,
        c.ValorMensalidade,
        c.DiaVencimento,
        c.CobrancaAtiva,
        c.DataInicioCobranca,
        c.CreatedAt
    };

    private static void ApplyClinicaFields(Clinica clinica, CreateClinicaDto dto)
    {
        clinica.Nome = dto.Nome.Trim();
        clinica.NomeFantasia = dto.NomeFantasia.Trim();
        clinica.NomeResponsavel = dto.NomeResponsavel.Trim();
        clinica.Cnpj = DigitsOnly(dto.Cnpj);
        clinica.Email = NormalizeEmail(dto.Email);
        clinica.Telefone = DigitsOnly(dto.Telefone);
        clinica.Rua = dto.Rua.Trim();
        clinica.Numero = dto.Numero.Trim();
        clinica.Bairro = dto.Bairro.Trim();
        clinica.Cidade = dto.Cidade.Trim();
        clinica.Estado = dto.Estado.Trim();
        clinica.Cep = DigitsOnly(dto.Cep);
    }

    private static void ApplyClinicaFields(Clinica clinica, UpdateClinicaDto dto)
    {
        clinica.Nome = dto.Nome.Trim();
        clinica.NomeFantasia = dto.NomeFantasia.Trim();
        clinica.NomeResponsavel = dto.NomeResponsavel.Trim();
        clinica.Cnpj = DigitsOnly(dto.Cnpj);
        clinica.Email = NormalizeEmail(dto.Email);
        clinica.Telefone = DigitsOnly(dto.Telefone);
        clinica.Rua = dto.Rua.Trim();
        clinica.Numero = dto.Numero.Trim();
        clinica.Bairro = dto.Bairro.Trim();
        clinica.Cidade = dto.Cidade.Trim();
        clinica.Estado = dto.Estado.Trim();
        clinica.Cep = DigitsOnly(dto.Cep);
    }

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
    private static string DigitsOnly(string value) => new(value.Where(char.IsDigit).ToArray());
}
