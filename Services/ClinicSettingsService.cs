using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public partial class ClinicSettingsService(AppDbContext db, IUsuarioLogadoService usuario) : IClinicSettingsService
{
    [GeneratedRegex(@"^#[A-Fa-f0-9]{6}$")]
    private static partial Regex HexColorRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    public async Task<Result<ClinicSettingsDto>> GetCurrentClinicSettingsAsync()
    {
        var clinic = await FindClinicAsync(usuario.ClinicaId);
        return clinic is null
            ? Result<ClinicSettingsDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.")
            : Result<ClinicSettingsDto>.Ok(Map(clinic));
    }

    public async Task<Result<ClinicSettingsDto>> UpdateCurrentClinicSettingsAsync(UpdateClinicSettingsRequest request)
        => await UpdateAsync(usuario.ClinicaId, request);

    public async Task<Result<ClinicSettingsDto>> GetClinicSettingsAsSuperAdminAsync(int clinicId)
    {
        var clinic = await FindClinicAsync(clinicId);
        return clinic is null
            ? Result<ClinicSettingsDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.")
            : Result<ClinicSettingsDto>.Ok(Map(clinic));
    }

    public async Task<Result<ClinicSettingsDto>> UpdateClinicSettingsAsSuperAdminAsync(int clinicId, UpdateClinicSettingsRequest request)
        => await UpdateAsync(clinicId, request);

    private async Task<Result<ClinicSettingsDto>> UpdateAsync(int clinicId, UpdateClinicSettingsRequest request)
    {
        var validationError = Validate(request);
        if (validationError is not null)
            return Result<ClinicSettingsDto>.Fail(ErrorCodes.InvalidValue, validationError);

        var clinic = await FindClinicAsync(clinicId);
        if (clinic is null)
            return Result<ClinicSettingsDto>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        clinic.DisplayName = request.DisplayName?.Trim();
        clinic.LogoUrl = request.LogoUrl?.Trim();
        clinic.PrimaryColor = request.PrimaryColor?.Trim();
        clinic.SecondaryColor = request.SecondaryColor?.Trim();
        clinic.AccentColor = request.AccentColor?.Trim();
        clinic.ContactEmail = request.ContactEmail?.Trim();
        clinic.ContactPhone = request.ContactPhone?.Trim();

        // ── Presença pública ─────────────────────────────────────────────────
        var slug = request.PublicSlug?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(slug))
        {
            if (await db.Clinicas.AnyAsync(c => c.PublicSlug == slug && c.Id != clinicId && !c.IsDeleted))
                return Result<ClinicSettingsDto>.Fail(ErrorCodes.DuplicateName, "Este slug público já está em uso.");
            clinic.PublicSlug = slug;
        }
        else if (request.PublicSlug is not null)
        {
            clinic.PublicSlug = null;
        }

        if (request.Description is not null) clinic.Description = request.Description.Trim();
        if (request.IsPublic.HasValue) clinic.IsPublic = request.IsPublic.Value;
        if (request.AcceptsAppointmentRequests.HasValue) clinic.AcceptsAppointmentRequests = request.AcceptsAppointmentRequests.Value;
        if (request.Latitude.HasValue) clinic.Latitude = request.Latitude;
        if (request.Longitude.HasValue) clinic.Longitude = request.Longitude;

        if (request.Address is not null)
        {
            clinic.Rua = request.Address.Rua?.Trim() ?? clinic.Rua;
            clinic.Numero = request.Address.Numero?.Trim() ?? clinic.Numero;
            clinic.Bairro = request.Address.Bairro?.Trim() ?? clinic.Bairro;
            clinic.Cidade = request.Address.Cidade?.Trim() ?? clinic.Cidade;
            clinic.Estado = request.Address.Estado?.Trim() ?? clinic.Estado;
            clinic.Cep = request.Address.Cep?.Trim() ?? clinic.Cep;
        }

        clinic.UpdatedByUserId = usuario.IsSuperAdmin ? null : usuario.UserId;

        await db.SaveChangesAsync();

        return Result<ClinicSettingsDto>.Ok(Map(clinic));
    }

    private Task<Clinica?> FindClinicAsync(int clinicId) =>
        db.Clinicas.FirstOrDefaultAsync(c => c.Id == clinicId && !c.IsDeleted);

    private static string? Validate(UpdateClinicSettingsRequest request)
    {
        if (request.DisplayName?.Trim().Length > 120)
            return "O nome de exibição deve ter no máximo 120 caracteres.";

        if (!string.IsNullOrWhiteSpace(request.LogoUrl))
        {
            var logoUrl = request.LogoUrl.Trim();
            if (logoUrl.Length > 500)
                return "A URL do logo deve ter no máximo 500 caracteres.";
            if (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return "A URL do logo é inválida.";
        }

        if (!IsValidColor(request.PrimaryColor))
            return "A cor primária deve estar no formato #RRGGBB.";
        if (!IsValidColor(request.SecondaryColor))
            return "A cor secundária deve estar no formato #RRGGBB.";
        if (!IsValidColor(request.AccentColor))
            return "A cor de destaque deve estar no formato #RRGGBB.";

        if (!string.IsNullOrWhiteSpace(request.ContactEmail))
        {
            var email = request.ContactEmail.Trim();
            if (email.Length > 160)
                return "O email de contato deve ter no máximo 160 caracteres.";
            if (!MailAddress.TryCreate(email, out _))
                return "O email de contato é inválido.";
        }

        if (request.ContactPhone?.Trim().Length > 30)
            return "O telefone de contato deve ter no máximo 30 caracteres.";

        if (!string.IsNullOrWhiteSpace(request.PublicSlug))
        {
            var slug = request.PublicSlug.Trim().ToLowerInvariant();
            if (slug.Length is < 3 or > 60)
                return "O slug público deve ter entre 3 e 60 caracteres.";
            if (!SlugRegex().IsMatch(slug))
                return "O slug público deve conter apenas letras minúsculas, números e hífens.";
        }

        if (request.Description?.Trim().Length > 2000)
            return "A descrição deve ter no máximo 2000 caracteres.";

        if (request.Latitude is < -90 or > 90)
            return "Latitude inválida.";
        if (request.Longitude is < -180 or > 180)
            return "Longitude inválida.";

        return null;
    }

    private static bool IsValidColor(string? color) =>
        string.IsNullOrWhiteSpace(color) || HexColorRegex().IsMatch(color.Trim());

    private static ClinicSettingsDto Map(Clinica clinic)
    {
        var name = clinic.NomeFantasia;
        return new ClinicSettingsDto
        {
            ClinicId = clinic.Id,
            Name = name,
            DisplayName = string.IsNullOrWhiteSpace(clinic.DisplayName) ? name : clinic.DisplayName,
            LogoUrl = clinic.LogoUrl,
            PrimaryColor = clinic.PrimaryColor,
            SecondaryColor = clinic.SecondaryColor,
            AccentColor = clinic.AccentColor,
            ContactEmail = string.IsNullOrWhiteSpace(clinic.ContactEmail) ? clinic.Email : clinic.ContactEmail,
            ContactPhone = string.IsNullOrWhiteSpace(clinic.ContactPhone) ? clinic.Telefone : clinic.ContactPhone,
            PublicSlug = clinic.PublicSlug,
            Description = clinic.Description,
            IsPublic = clinic.IsPublic,
            AcceptsAppointmentRequests = clinic.AcceptsAppointmentRequests,
            Latitude = clinic.Latitude,
            Longitude = clinic.Longitude,
            LikeCount = clinic.LikeCount,
            Address = new ClinicAddressDto
            {
                Rua = clinic.Rua,
                Numero = clinic.Numero,
                Bairro = clinic.Bairro,
                Cidade = clinic.Cidade,
                Estado = clinic.Estado,
                Cep = clinic.Cep,
            }
        };
    }
}
