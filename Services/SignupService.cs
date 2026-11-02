using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Signup;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class SignupService(
    AppDbContext db,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<SignupService> logger)
{
    public async Task SignupAsync(SignupRequestDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email && !user.IsDeleted))
            return;

        var clinic = new Clinica
        {
            Nome = dto.ClinicName.Trim(),
            NomeFantasia = dto.ClinicName.Trim(),
            NomeResponsavel = dto.OwnerName.Trim(),
            Email = email,
            Telefone = dto.Phone.Trim(),
            Plan = ClinicPlan.Free,
            IsHubApproved = false,
        };
        var admin = new User
        {
            Clinica = clinic,
            Name = dto.OwnerName.Trim(),
            Email = email,
            Role = UserRole.Administrador,
            IsActive = false,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
        };
        var token = SetInvitation(admin);
        db.Clinicas.Add(clinic);
        db.Users.Add(admin);
        db.CommercialHistoryEvents.Add(new CommercialHistoryEvent
        {
            Clinica = clinic,
            Type = CommercialHistoryEventType.ClinicCreated,
            Description = "Cadastro self-service",
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return; // e-mail já cadastrado em outra requisição: mesma resposta, sem revelar a conta
        }

        await SendConfirmationAsync(clinic, admin, token);
    }

    public async Task ResendAsync(ResendSignupDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var admin = await db.Users.Include(user => user.Clinica).FirstOrDefaultAsync(user =>
            user.Email == email
            && user.Role == UserRole.Administrador
            && !user.IsActive
            && !user.IsDeleted
            && user.InvitationTokenHash != null);
        if (admin is null)
            return;

        var token = SetInvitation(admin);
        await db.SaveChangesAsync();
        await SendConfirmationAsync(admin.Clinica, admin, token);
    }

    public async Task<Result<string>> ConfirmAsync(string token)
    {
        var hash = Hash(token);
        var now = DateTime.UtcNow;
        var admin = await db.Users.FirstOrDefaultAsync(user =>
            user.InvitationTokenHash == hash
            && user.InvitationExpiresAt > now
            && !user.IsDeleted
            && !user.IsActive
            && user.Role == UserRole.Administrador
            && user.Clinica.IsActive
            && !user.Clinica.IsDeleted);
        if (admin is null)
            return Result<string>.Fail(ErrorCodes.InvalidToken, "Token inválido ou expirado.");

        admin.IsActive = true;
        admin.InvitationTokenHash = null;
        admin.InvitationExpiresAt = null;
        await db.SaveChangesAsync();
        return Result<string>.Ok(admin.Email);
    }

    private static string SetInvitation(User user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.InvitationTokenHash = Hash(token);
        user.InvitationExpiresAt = DateTime.UtcNow.AddHours(72);
        return token;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task SendConfirmationAsync(Clinica clinic, User admin, string token)
    {
        var frontend = (configuration["FRONTEND_URL"] ?? "https://cliniqcare.com.br").TrimEnd('/');
        var link = WebUtility.HtmlEncode($"{frontend}/cadastro/confirmar?token={token}");
        var name = WebUtility.HtmlEncode(admin.Name);
        var clinicName = WebUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(clinic.NomeFantasia) ? clinic.Nome : clinic.NomeFantasia);
        var body = $"""
            <!doctype html><html lang="pt-BR"><body>
            <h1>Confirme seu cadastro no Cliniq</h1>
            <p>Olá, {name}. Recebemos o cadastro da clínica {clinicName}.</p>
            <p><a href="{link}">Confirmar meu e-mail e acessar o Cliniq</a></p>
            <p>Este link é de uso único e expira em 72 horas. Se você não fez este cadastro, ignore este e-mail.</p>
            </body></html>
            """;
        try
        {
            await emailSender.SendAsync(admin.Email, "Confirme seu cadastro no Cliniq", body);
        }
        catch (Exception)
        {
            logger.LogError("Falha no envio do e-mail de confirmação do cadastro do usuário {UserId}.", admin.Id);
        }
    }
}
