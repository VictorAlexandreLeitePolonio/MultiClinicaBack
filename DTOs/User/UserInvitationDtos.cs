using System.ComponentModel.DataAnnotations;
using MultiClinica.API.Models;

namespace MultiClinica.API.DTOs.User;

public class InviteUserDto
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [EnumDataType(typeof(UserRole))]
    public UserRole Role { get; set; } = UserRole.Profissional;
}

public class AcceptUserInvitationDto : IValidatableObject
{
    [Required, StringLength(128)]
    public string Token { get; set; } = string.Empty;
    [Required, StringLength(64, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(Password) > 72)
            yield return new ValidationResult("A senha é muito longa. Use menos caracteres.", [nameof(Password)]);
    }
}

public record UserInvitationResponseDto(int UserId, bool EmailSent);
