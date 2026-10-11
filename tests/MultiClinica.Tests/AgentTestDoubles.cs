using MultiClinica.API.Models;

namespace MultiClinica.Tests;

public static class AgentSeed
{
    public static async Task<(int ClinicId, int AdminId)> ClinicWithAdminAsync(MultiClinicaFactory app,
        string email = "agent-admin@test.local", long? telegramChatId = null)
    {
        var ids = (0, 0);
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Agente", NomeFantasia = "Clínica Agente",
                Email = $"clinic-{email}", TimeZoneId = "America/Sao_Paulo", AppointmentSlotDurationMinutes = 60 };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            var admin = new User { ClinicaId = clinic.Id, Name = "Victor", Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador,
                TelegramChatId = telegramChatId };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            ids = (clinic.Id, admin.Id);
        });
        return ids;
    }
}
