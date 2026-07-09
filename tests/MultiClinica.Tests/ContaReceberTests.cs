using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ContaReceberTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<int> SeedAsync(MultiClinicaFactory app)
    {
        var pacienteId = 0;
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            var paciente = new Patient { ClinicaId = clinica.Id, Name = "Paciente A" };
            db.Patients.Add(paciente);
            await db.SaveChangesAsync();
            pacienteId = paciente.Id;

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin",
                    Email = "admin@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                });
            await db.SaveChangesAsync();
        });
        return pacienteId;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CriarContaReceber_ValorOriginalZero_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var pacienteId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");

        var response = await client.PostAsJsonAsync("/api/contas-receber", new CreateContaReceberDto
        {
            PacienteId = pacienteId,
            Descricao = "Teste",
            ValorOriginal = 0,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarContaReceber_RecepcaoComDesconto_IgnoraDescontoEZera()
    {
        await using var app = new MultiClinicaFactory();
        var pacienteId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep@a.local");

        var response = await client.PostAsJsonAsync("/api/contas-receber", new CreateContaReceberDto
        {
            PacienteId = pacienteId,
            Descricao = "Consulta",
            ValorOriginal = 100,
            ValorDesconto = 20,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });
        var body = await response.Content.ReadFromJsonAsync<ContaReceberResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0, body!.ValorDesconto);
        Assert.Equal(100, body.ValorTotal);
    }

    [Fact]
    public async Task CancelarConta_ComValorRecebido_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var pacienteId = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/contas-receber", new CreateContaReceberDto
        {
            PacienteId = pacienteId,
            Descricao = "Consulta",
            ValorOriginal = 100,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });
        var conta = await createResponse.Content.ReadFromJsonAsync<ContaReceberResponseDto>(JsonOptions);
        await app.SeedAsync(async db =>
        {
            var entity = await db.ContasReceber.FirstAsync(c => c.Id == conta!.Id);
            entity.ValorRecebido = 50;
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync($"/api/contas-receber/{conta!.Id}/cancelar",
            new MotivoDto { Motivo = "Teste" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelarConta_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        var pacienteId = await SeedAsync(app);
        using var adminClient = app.CreateClient();
        await LoginAsync(adminClient, "admin@a.local");
        var createResponse = await adminClient.PostAsJsonAsync("/api/contas-receber", new CreateContaReceberDto
        {
            PacienteId = pacienteId,
            Descricao = "Consulta",
            ValorOriginal = 100,
            DataEmissao = DateTime.UtcNow,
            DataVencimento = DateTime.UtcNow.AddDays(5)
        });
        var conta = await createResponse.Content.ReadFromJsonAsync<ContaReceberResponseDto>(JsonOptions);

        using var recepClient = app.CreateClient();
        await LoginAsync(recepClient, "recep@a.local");

        var response = await recepClient.PostAsJsonAsync($"/api/contas-receber/{conta!.Id}/cancelar",
            new MotivoDto { Motivo = "Teste" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetInadimplencia_Recepcao_Retorna200()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep@a.local");

        var response = await client.GetAsync("/api/contas-receber/inadimplencia");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
