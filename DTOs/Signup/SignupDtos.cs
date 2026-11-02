using System.ComponentModel.DataAnnotations;

namespace MultiClinica.API.DTOs.Signup;

public class SignupRequestDto : IValidatableObject
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string ClinicName { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string OwnerName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 1)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(Password) > 72)
            yield return new ValidationResult("A senha é muito longa. Use menos caracteres.", [nameof(Password)]);
    }
}

public class ResendSignupDto
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
}

public class ConfirmSignupDto
{
    [Required, StringLength(128)]
    public string Token { get; set; } = string.Empty;
}
