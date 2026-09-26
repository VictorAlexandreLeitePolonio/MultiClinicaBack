using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.User;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class UserInvitationService(
    AppDbContext db,
    IUsuarioLogadoService currentUser,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<UserInvitationService> logger)
{
    public async Task<Result<UserInvitationResponseDto>> InviteAsync(InviteUserDto dto)
    {
        if (dto.Role is not (UserRole.Profissional or UserRole.Recepcao))
            return Result<UserInvitationResponseDto>.Fail(ErrorCodes.Forbidden, "Convide um profissional ou recepcionista.");
        var email = dto.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email))
            return Result<UserInvitationResponseDto>.Fail(ErrorCodes.DuplicateEmail, "E-mail já cadastrado. Reenvie o convite pela lista de usuários.");
        var user = new User
        {
            ClinicaId = currentUser.ClinicaId,
            Name = dto.Name.Trim(),
            Email = email,
            Role = dto.Role,
            IsActive = false,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
            CreatedByUserId = currentUser.UserId,
        };
        var token = SetInvitation(user);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            return Result<UserInvitationResponseDto>.Fail(ErrorCodes.DuplicateEmail, "E-mail já cadastrado.");
        }
        return Result<UserInvitationResponseDto>.Ok(new(user.Id, await SendAsync(user, token)));
    }

    public async Task<Result<UserInvitationResponseDto>> ResendAsync(int id)
    {
        var user = await db.Users.FirstOrDefaultAsync(user => user.Id == id
            && user.ClinicaId == currentUser.ClinicaId && !user.IsDeleted
            && !user.IsActive && user.InvitationTokenHash != null);
        if (user is null)
            return Result<UserInvitationResponseDto>.Fail(ErrorCodes.NotFound, "Convite pendente não encontrado.");
        var token = SetInvitation(user);
        user.UpdatedByUserId = currentUser.UserId;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<UserInvitationResponseDto>.Fail(ErrorCodes.NotFound, "O convite mudou. Atualize a lista de usuários.");
        }
        return Result<UserInvitationResponseDto>.Ok(new(user.Id, await SendAsync(user, token)));
    }

    public async Task<bool> AcceptAsync(AcceptUserInvitationDto dto)
    {
        var hash = Hash(dto.Token);
        var now = DateTime.UtcNow;
        var query = db.Users.Where(user => user.InvitationTokenHash == hash
            && user.InvitationExpiresAt > now && !user.IsDeleted && !user.IsActive
            && (user.Role == UserRole.Profissional || user.Role == UserRole.Recepcao)
            && user.Clinica.IsActive && !user.Clinica.IsDeleted && !user.Clinica.IsBlockedByBilling);
        var user = await query.FirstOrDefaultAsync();
        if (user is null) return false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        user.IsActive = true;
        user.InvitationTokenHash = null;
        user.InvitationExpiresAt = null;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        return true;
    }

    private static string SetInvitation(User user)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.InvitationTokenHash = Hash(token);
        user.InvitationExpiresAt = DateTime.UtcNow.AddHours(72);
        return token;
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<bool> SendAsync(User user, string token)
    {
        var frontend = (configuration["FRONTEND_URL"] ?? "https://cliniqcare.com.br").TrimEnd('/');
        var link = WebUtility.HtmlEncode($"{frontend}/convite/aceitar?token={token}");
        var name = WebUtility.HtmlEncode(user.Name);
        var clinic = await db.Clinicas.Where(clinic => clinic.Id == user.ClinicaId)
            .Select(clinic => clinic.NomeFantasia == "" ? clinic.Nome : clinic.NomeFantasia).SingleAsync();
        var clinicName = WebUtility.HtmlEncode(clinic);
        var body = $"""
            <!doctype html><html lang="pt-BR"><body>
            <h1>Seu convite para o Cliniq</h1>
            <p>Olá, {name}. Você foi convidado para a equipe da clínica {clinicName} no Cliniq.</p>
            <p><a href="{link}">Aceitar convite e definir minha senha</a></p>
            <p>Este link é de uso único e expira em 72 horas. Se você não esperava este convite, ignore este e-mail.</p>
            </body></html>
            """;
        try
        {
            await emailSender.SendAsync(user.Email, "Defina sua senha para acessar o Cliniq", body);
            return true;
        }
        catch (Exception)
        {
            logger.LogError("Falha no envio do convite do usuário {UserId}. O administrador pode reenviar.", user.Id);
            return false;
        }
    }
}
