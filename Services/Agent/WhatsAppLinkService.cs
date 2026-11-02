using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Agent;

// Mesmo padrão do convite de usuário: só o hash do código fica no banco, uso único, expira rápido.
public sealed class WhatsAppLinkService(AppDbContext db, IOptions<WhatsAppOptions> options, TimeProvider time,
    IWhatsAppClient whatsApp, ILogger<WhatsAppLinkService> logger)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public async Task<string> CreateLinkAsync(int userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var code = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)); // 43 chars, cabe no texto do wa.me
        user.WhatsAppLinkCodeHash = Hash(code);
        user.WhatsAppLinkExpiresAt = time.GetUtcNow().UtcDateTime.Add(Lifetime);
        await db.SaveChangesAsync();
        return $"https://wa.me/{options.Value.DisplayNumber}?text={Uri.EscapeDataString("conectar " + code)}";
    }

    public async Task<bool> IsConnectedAsync(int userId)
        => await db.Users.AnyAsync(u => u.Id == userId && u.WhatsAppPhone != null);

    public async Task UnlinkAsync(int userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.WhatsAppPhone = null;
        await db.SaveChangesAsync();
    }

    public async Task<bool> CompleteAsync(string from, string code)
    {
        var hash = Hash(code.Trim());
        var now = time.GetUtcNow().UtcDateTime;
        var user = await db.Users.FirstOrDefaultAsync(u => u.WhatsAppLinkCodeHash == hash
            && u.WhatsAppLinkExpiresAt > now && u.IsActive && !u.IsDeleted && u.Role == UserRole.Administrador);
        if (user is null)
        {
            await ReplyAsync(from, "Link inválido ou expirado. Gere um novo em Configurações → Conectar WhatsApp.");
            return false;
        }

        // Um número do WhatsApp pertence a um usuário só. Libera antes de gravar o novo dono: o índice único de
        // WhatsAppPhone é checado a cada UPDATE no Postgres, e o EF não garante a ordem dos UPDATEs no mesmo SaveChanges.
        var previous = await db.Users.Where(u => u.WhatsAppPhone == from && u.Id != user.Id).ToListAsync();
        if (previous.Count > 0)
        {
            foreach (var p in previous) p.WhatsAppPhone = null;
            await db.SaveChangesAsync();
        }
        user.WhatsAppPhone = from;
        user.WhatsAppLinkCodeHash = null;
        user.WhatsAppLinkExpiresAt = null;
        await db.SaveChangesAsync();

        await ReplyAsync(from,
            $"Pronto, {user.Name}! Sou o assistente da sua clínica. Pode pedir, por exemplo: \"agenda a Cida terça às 9h\" ou \"o que tenho amanhã?\".");
        return true;
    }

    // Falha de envio não pode virar 500: a Meta reentregaria a mensagem "conectar" já consumida e o usuário veria "inválido" com o vínculo feito.
    private async Task ReplyAsync(string to, string text)
    {
        try { await whatsApp.SendTextAsync(to, text, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Falha ao responder o vínculo do WhatsApp"); }
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
