namespace ProjetoLP.API.DTOs.Auth;

// Dados recebidos no login — email e senha em texto puro.
// A senha é verificada contra o hash BCrypt salvo no banco.
public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
