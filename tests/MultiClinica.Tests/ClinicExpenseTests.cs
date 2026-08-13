using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ClinicExpenseTests
{
    private static async Task<string> SeedAdminAsync(MultiClinicaFactory app, string email)
    {
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica Despesas", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinica.Id,
                Name = "Admin",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            });
            await db.SaveChangesAsync();
        });
        return email;
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateExpense_DadosValidos_Retorna201()
    {
        await using var app = new MultiClinicaFactory();
        var email = await SeedAdminAsync(app, "admin.expense1@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/financial/expenses", new CreateClinicExpenseDto
        {
            Title = "Aluguel",
            Amount = 2500,
            Date = DateTime.UtcNow,
            Description = "Aluguel do mês"
        });
        var expense = await response.Content.ReadFromJsonAsync<ClinicExpenseResponseDto>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Aluguel", expense!.Title);
        Assert.Equal(2500, expense.Amount);
    }

    [Fact]
    public async Task CreateExpense_ValorZero_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var email = await SeedAdminAsync(app, "admin.expense2@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/financial/expenses", new CreateClinicExpenseDto
        {
            Title = "Gasto inválido",
            Amount = 0,
            Date = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExpense_Existente_AtualizaValores()
    {
        await using var app = new MultiClinicaFactory();
        var email = await SeedAdminAsync(app, "admin.expense3@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var createResponse = await client.PostAsJsonAsync("/api/financial/expenses", new CreateClinicExpenseDto
        {
            Title = "Luz",
            Amount = 300,
            Date = DateTime.UtcNow
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ClinicExpenseResponseDto>();

        var updateResponse = await client.PutAsJsonAsync($"/api/financial/expenses/{created!.Id}", new UpdateClinicExpenseDto
        {
            Title = "Luz e água",
            Amount = 450,
            Date = created.Date
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<ClinicExpenseResponseDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Luz e água", updated!.Title);
        Assert.Equal(450, updated.Amount);
    }

    [Fact]
    public async Task DeleteExpense_Existente_RemoveDaListagem()
    {
        await using var app = new MultiClinicaFactory();
        var email = await SeedAdminAsync(app, "admin.expense4@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var createResponse = await client.PostAsJsonAsync("/api/financial/expenses", new CreateClinicExpenseDto
        {
            Title = "Material de limpeza",
            Amount = 100,
            Date = DateTime.UtcNow
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ClinicExpenseResponseDto>();

        var deleteResponse = await client.DeleteAsync($"/api/financial/expenses/{created!.Id}");
        var getResponse = await client.GetAsync($"/api/financial/expenses/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetBalance_ComGastoManual_SomaNoTotalOutcomeEReduzResultado()
    {
        await using var app = new MultiClinicaFactory();
        var email = await SeedAdminAsync(app, "admin.expense5@a.local");
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var now = DateTime.UtcNow;
        await client.PostAsJsonAsync("/api/financial/expenses", new CreateClinicExpenseDto
        {
            Title = "Aluguel",
            Amount = 2000,
            Date = now
        });

        var response = await client.GetAsync("/api/financial/balance");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(2000, balance!.Money.ManualExpenseCost);
        Assert.Equal(1, balance.Money.ManualExpenseCount);
        Assert.Equal(2000, balance.Money.TotalOutcome);
        Assert.Equal(-2000, balance.Money.EstimatedProfit);
        Assert.Contains(balance.RecentMovements, m => m.Source == "ManualExpense" && m.Type == "ClinicExpense");
    }
}
