using MultiClinica.API.Models;

namespace MultiClinica.Tests;

public static class AgentSeed
{
    public static async Task<(int ClinicId, int AdminId)> ClinicWithAdminAsync(MultiClinicaFactory app,
        string email = "agent-admin@test.local", long? telegramChatId = null, string? whatsAppPhone = null)
    {
        var ids = (0, 0);
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Agente", NomeFantasia = "Clínica Agente",
                Email = $"clinic-{email}", TimeZoneId = "America/Sao_Paulo", AppointmentSlotDurationMinutes = 60,
                Plan = ClinicPlan.Pro };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            var admin = new User { ClinicaId = clinic.Id, Name = "Victor", Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador,
                TelegramChatId = telegramChatId, WhatsAppPhone = whatsAppPhone };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            ids = (clinic.Id, admin.Id);
        });
        return ids;
    }
}

public sealed class FakeChatModel : MultiClinica.API.Services.Agent.IChatModel
{
    public Queue<Func<MultiClinica.API.Services.Agent.AgentChatResult>> Script { get; } = new();
    public List<(string Model, List<MultiClinica.API.Services.Agent.AgentChatMessage> Messages)> Requests { get; } = [];
    // Roda dentro da chamada, antes da resposta roteirizada: simula mensagem que chega durante o LLM.
    public Func<Task>? OnCall { get; set; }

    public FakeChatModel Reply(string text)
    {
        Script.Enqueue(() => new(text, [], 10, 5));
        return this;
    }

    public FakeChatModel CallTool(string name, string argsJson, string id = "call_1")
    {
        Script.Enqueue(() => new(null, [new(id, name, argsJson)], 10, 5));
        return this;
    }

    public FakeChatModel Throw()
    {
        Script.Enqueue(() => throw new HttpRequestException("LLM fora do ar"));
        return this;
    }

    public async Task<MultiClinica.API.Services.Agent.AgentChatResult> CompleteAsync(string model,
        IReadOnlyList<MultiClinica.API.Services.Agent.AgentChatMessage> messages,
        IReadOnlyList<MultiClinica.API.Services.Agent.AgentToolDefinition> tools, CancellationToken ct)
    {
        Requests.Add((model, messages.ToList()));
        if (OnCall is not null) await OnCall();
        if (Script.Count == 0) throw new InvalidOperationException("FakeChatModel sem resposta roteirizada.");
        return Script.Dequeue()();
    }
}

public sealed class FakeTelegramClient : MultiClinica.API.Services.Agent.ITelegramClient
{
    public List<(long ChatId, string Text)> Sent { get; } = [];

    public Task SendMessageAsync(long chatId, string text, CancellationToken ct)
    {
        Sent.Add((chatId, text));
        return Task.CompletedTask;
    }
}

public sealed class FakeWhatsAppClient : MultiClinica.API.Services.Agent.IWhatsAppClient
{
    public List<(string To, string Text)> Sent { get; } = [];
    public bool Fail { get; set; }

    public Task SendTextAsync(string to, string text, CancellationToken ct)
    {
        if (Fail) throw new HttpRequestException("WhatsApp fora do ar");
        Sent.Add((to, text));
        return Task.CompletedTask;
    }
}
