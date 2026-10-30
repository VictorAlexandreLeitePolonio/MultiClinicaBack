using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using Npgsql;
using Xunit;

namespace MultiClinica.Tests;

public class UsageServiceTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static async Task<int> SeedClinicAsync(AppDbContext db, ClinicPlan plan)
    {
        var clinic = new Clinica { Nome = "C", Plan = plan };
        db.Clinicas.Add(clinic);
        await db.SaveChangesAsync();
        return clinic.Id;
    }

    [Fact]
    public void Periodo_usa_fuso_de_Sao_Paulo()
    {
        // 01/12 02:00 UTC ainda é 30/11 23:00 em São Paulo
        Assert.Equal("2026-11", UsageService.CurrentPeriod(new DateTime(2026, 12, 1, 2, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("2026-12", UsageService.CurrentPeriod(new DateTime(2026, 12, 1, 4, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task Consome_ate_o_limite_e_recusa_depois()
    {
        await using var db = NewDb();
        var id = await SeedClinicAsync(db, ClinicPlan.Pro); // 300 conversas
        var service = new UsageService(db);
        Assert.True(await service.TryConsumeAsync(id, UsageMetric.WhatsAppConversations, 299));
        Assert.True(await service.TryConsumeAsync(id, UsageMetric.WhatsAppConversations, 1));
        Assert.False(await service.TryConsumeAsync(id, UsageMetric.WhatsAppConversations, 1));
    }

    [Fact]
    public async Task Plano_sem_cota_recusa()
    {
        await using var db = NewDb();
        var id = await SeedClinicAsync(db, ClinicPlan.Pro);
        Assert.False(await new UsageService(db).TryConsumeAsync(id, UsageMetric.TranscriptionMinutes, 1));
    }

    [Fact]
    public async Task Pacote_soma_no_mes_corrente()
    {
        await using var db = NewDb();
        var id = await SeedClinicAsync(db, ClinicPlan.Pro);
        var service = new UsageService(db);
        Assert.True(await service.TryConsumeAsync(id, UsageMetric.WhatsAppConversations, 300));
        await service.GrantExtraAsync(id, UsageMetric.WhatsAppConversations, 200);
        Assert.True(await service.TryConsumeAsync(id, UsageMetric.WhatsAppConversations, 200));
        Assert.False(await service.TryConsumeAsync(id, UsageMetric.WhatsAppConversations, 1));

        var usage = await service.GetAsync(id);
        var conversations = usage.Metrics.Single(m => m.Metric == UsageMetric.WhatsAppConversations);
        Assert.Equal(500, conversations.Used);
        Assert.Equal(300, conversations.PlanLimit);
        Assert.Equal(200, conversations.ExtraGranted);
        Assert.Equal(500, conversations.Total);
    }

    [Fact]
    public async Task Pacote_de_mes_anterior_nao_vale()
    {
        await using var db = NewDb();
        var id = await SeedClinicAsync(db, ClinicPlan.Pro);
        db.ClinicUsages.Add(new ClinicUsage { ClinicaId = id, Period = "2000-01", Metric = UsageMetric.WhatsAppConversations, ExtraGranted = 999 });
        await db.SaveChangesAsync();
        var usage = await new UsageService(db).GetAsync(id);
        Assert.Equal(0, usage.Metrics.Single(m => m.Metric == UsageMetric.WhatsAppConversations).ExtraGranted);
    }

    [Fact]
    public async Task Consumo_concorrente_no_postgres_nao_ultrapassa_o_limite()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("MULTICLINICA_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(adminConnectionString)) return;

        var databaseName = $"multiclinica_usage_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName };
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Clinicas.Add(new Clinica { Id = 1, Nome = "Teste", Plan = ClinicPlan.Pro });
                db.ClinicUsages.Add(new ClinicUsage
                {
                    ClinicaId = 1,
                    Period = UsageService.CurrentPeriod(DateTime.UtcNow),
                    Metric = UsageMetric.WhatsAppConversations,
                    Used = 295,
                });
                await db.SaveChangesAsync();
            }

            async Task<bool> Consume()
            {
                await using var db = new AppDbContext(options);
                return await new UsageService(db).TryConsumeAsync(1, UsageMetric.WhatsAppConversations, 1);
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Consume()));
            Assert.Equal(5, results.Count(result => result));
            await using var verify = new AppDbContext(options);
            Assert.Equal(300, (await verify.ClinicUsages.SingleAsync()).Used);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Endpoint_de_uso_devolve_periodo_e_metricas()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica", Plan = ClinicPlan.Pro };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id, Name = "Admin", Email = "admin@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador,
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@test.local", password = "secret123" }))
            .EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/billing/usage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(7, body.RootElement.GetProperty("period").GetString()!.Length);
        Assert.Equal(2, body.RootElement.GetProperty("metrics").GetArrayLength());
        var conversations = body.RootElement.GetProperty("metrics").EnumerateArray()
            .Single(metric => metric.GetProperty("metric").GetString() == "WhatsAppConversations");
        Assert.Equal(300, conversations.GetProperty("planLimit").GetInt32());
    }
}
