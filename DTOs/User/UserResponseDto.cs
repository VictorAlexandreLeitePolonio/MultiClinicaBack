namespace ProjetoLP.API.DTOs.User;

using ProjetoLP.API.Models;

// Dados retornados ao cliente nas respostas da API.
// PasswordHash nunca aparece aqui — nem criptografado.
// Navigation properties (Appointments, Payments) também omitidas — evita loops de serialização.
public class UserResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}
