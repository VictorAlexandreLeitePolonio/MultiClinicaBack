namespace ProjetoLP.API.DTOs.User;

// Dados permitidos na atualização de um usuário.
// Role não está aqui — um Patient sempre será Patient e um Admin sempre será Admin.
// Password não está aqui — troca de senha é feita em endpoint dedicado.
public class UpdateUserDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
