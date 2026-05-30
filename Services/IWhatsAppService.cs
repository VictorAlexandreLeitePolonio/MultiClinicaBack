namespace ProjetoLP.API.Services;

public interface IWhatsAppService
{
    // Retorna true se a Evolution API respondeu com sucesso, false em caso de falha.
    Task<bool> SendTextAsync(string phone, string message);
}
