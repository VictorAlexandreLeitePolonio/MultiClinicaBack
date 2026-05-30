namespace ProjetoLP.API.DTOs.User;

using ProjetoLP.API.Models;

// Dados recebidos na criação de um usuário.
// Não inclui Id, CreatedAt, UpdatedAt — gerados pelo servidor.
public class CreateUserDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // Senha em texto puro — será transformada em hash BCrypt no controller antes de salvar.
    public string Password { get; set; } = string.Empty;

    // Role padrão é Patient — pode ser alterado apenas na criação.
    public UserRole Role { get; set; } = UserRole.Fisio;
}
