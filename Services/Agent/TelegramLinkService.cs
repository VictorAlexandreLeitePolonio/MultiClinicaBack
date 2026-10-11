using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Agent;

// Mesmo padrão do convite de usuário: só o hash do código fica no banco, uso único, expira rápido.
public sealed class TelegramLinkService(AppDbContext db, IOptions<TelegramOptions> options, TimeProvider time,
    ITelegramClient telegram, ILogger<TelegramLinkService> logger)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public async Task<string> CreateLinkAsync(int userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var code = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)); // 43 chars, aceito pelo /start
        user.TelegramLinkCodeHash = Hash(code);
        user.TelegramLinkExpiresAt = time.GetUtcNow().UtcDateTime.Add(Lifetime);
        await db.SaveChangesAsync();
        return $"https://t.me/{options.Value.BotUsername}?start={code}";
    }

    public async Task<bool> IsConnectedAsync(int userId)
        => await db.Users.AnyAsync(u => u.Id == userId && u.TelegramChatId != null);

    public async Task UnlinkAsync(int userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.TelegramChatId = null;
        await db.SaveChangesAsync();
    }

    public async Task<bool> CompleteAsync(long chatId, string code)
    {
        var hash = Hash(code.Trim());
        var now = time.GetUtcNow().UtcDateTime;
        var user = await db.Users.FirstOrDefaultAsync(u => u.TelegramLinkCodeHash == hash
            && u.TelegramLinkExpiresAt > now && u.IsActive && !u.IsDeleted && u.Role == UserRole.Administrador);
        if (user is null)
        {
            await ReplyAsync(chatId, "Link inválido ou expirado. Gere um novo em Configurações → Conectar Telegram.");
            return false;
        }

        // Um chat do Telegram pertence a um usuário só. Libera antes de gravar o novo dono: o índice único de
        // TelegramChatId é checado a cada UPDATE no Postgres, e o EF não garante a ordem dos UPDATEs no mesmo SaveChanges.
        var previous = await db.Users.Where(u => u.TelegramChatId == chatId && u.Id != user.Id).ToListAsync();
        if (previous.Count > 0)
        {
            foreach (var p in previous) p.TelegramChatId = null;
            await db.SaveChangesAsync();
        }
        user.TelegramChatId = chatId;
        user.TelegramLinkCodeHash = null;
        user.TelegramLinkExpiresAt = null;
        await db.SaveChangesAsync();

        await ReplyAsync(chatId,
            $"Pronto, {user.Name}! Sou o assistente da sua clínica. Pode pedir, por exemplo: \"agenda a Cida terça às 9h\" ou \"o que tenho amanhã?\".");
        return true;
    }

    // Falha de envio não pode virar 500: o Telegram reentregaria o /start já consumido e o usuário veria "inválido" com o vínculo feito.
    private async Task ReplyAsync(long chatId, string text)
    {
        try { await telegram.SendMessageAsync(chatId, text, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Falha ao responder o vínculo do Telegram"); }
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
