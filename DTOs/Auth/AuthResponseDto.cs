namespace MultiClinica.API.DTOs.Auth;

public sealed class AuthResponseDto
{
    public UserDto User { get; set; } = null!;
    public AuthTenantDto? Tenant { get; set; }
    public List<string> Permissions { get; set; } = [];
}
