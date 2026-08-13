using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class FinancialBalanceTests
{
    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<(Clinica Clinica, User Admin, Plans Plan)> SeedClinicaAsync(
        MultiClinicaFactory app, string clinicName, string adminEmail)
    {
        Clinica clinica = null!;
        User admin = null!;
        Plans plan = null!;

        await app.SeedAsync(async db =>
        {
            clinica = new Clinica { Nome = clinicName, NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            admin = new User
            {
                ClinicaId = clinica.Id,
                Name = "Admin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            };
            db.Users.Add(admin);

            plan = new Plans
            {
                ClinicaId = clinica.Id,
                Name = "Mensal",
                Valor = 150,
                TipoPlano = TipoPlano.Mensal,
                TipoSessao = TipoSessao.Fisioterapia
            };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
        });

        return (clinica, admin, plan);
    }

    [Fact]
    public async Task GetBalance_ClinicWithData_ReturnsOperationalBalance()
    {
        await using var app = new MultiClinicaFactory();
        var (clinica, admin, plan) = await SeedClinicaAsync(app, "Clinica A", "admin.balance1@a.local");
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        await app.SeedAsync(async db =>
        {
            var patient = new Patient { ClinicaId = clinica.Id, Name = "Paciente", IsActive = true };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            db.Appointments.Add(new Appointment
            {
                ClinicaId = clinica.Id,
                UserId = admin.Id,
                PatientId = patient.Id,
                AppointmentDate = monthStart.AddDays(1),
                Status = AppointmentStatus.Completed
            });

            db.Payments.Add(new Payment
            {
                ClinicaId = clinica.Id,
                PatientId = patient.Id,
                UserId = admin.Id,
                PlanId = plan.Id,
                ReferenceMonth = monthStart.ToString("MM-yyyy"),
                Amount = 150,
                PaymentMethod = "Pix",
                Status = PaymentStatus.Paid,
                PaidAt = monthStart.AddDays(1)
            });

            var produto = new Produto
            {
                ClinicaId = clinica.Id,
                Nome = "Creme",
                ValorCompra = 10,
                ValorVenda = 25,
                QuantidadeAtual = 1,
                QuantidadeMinima = 5
            };
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();

            db.MovimentacoesEstoque.Add(new MovimentacaoEstoque
            {
                ClinicaId = clinica.Id,
                ProdutoId = produto.Id,
                Tipo = TipoMovimentacaoEstoque.Venda,
                Quantidade = 1,
                QuantidadeAnterior = 2,
                QuantidadeAtual = 1,
                UnitValue = 25,
                TotalValue = 25,
                UsuarioId = admin.Id,
                CreatedAt = monthStart.AddDays(2)
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance1@a.local");

        var response = await client.GetAsync("/api/financial/balance");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, balance!.Patients.Active);
        Assert.Equal(1, balance.Appointments.Completed);
        Assert.Equal(150, balance.Money.AppointmentIncome);
        Assert.Equal(25, balance.Money.ProductSalesIncome);
        Assert.Equal(175, balance.Money.TotalIncome);
        Assert.Equal(1, balance.Stock.ProductsBelowMinimum);
        Assert.Single(balance.Stock.LowStockProducts);
        Assert.NotEmpty(balance.RecentMovements);
    }

    [Fact]
    public async Task GetBalance_WithoutPeriod_UsesCurrentMonth()
    {
        await using var app = new MultiClinicaFactory();
        var (_, admin, _) = await SeedClinicaAsync(app, "Clinica Sem Periodo", "admin.balance2@a.local");
        var now = DateTime.UtcNow;
        var expectedStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedEnd = expectedStart.AddMonths(1).AddDays(-1);

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance2@a.local");

        var response = await client.GetAsync("/api/financial/balance");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedStart, balance!.Period.StartDate);
        Assert.Equal(expectedEnd, balance.Period.EndDate);
    }

    [Fact]
    public async Task GetBalance_WithPeriod_ReturnsPeriodData()
    {
        await using var app = new MultiClinicaFactory();
        var (clinica, admin, _) = await SeedClinicaAsync(app, "Clinica Periodo", "admin.balance3@a.local");
        var insidePeriod = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var outsidePeriod = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);

        await app.SeedAsync(async db =>
        {
            var patientInside = new Patient { ClinicaId = clinica.Id, Name = "Dentro", IsActive = true };
            var patientOutside = new Patient { ClinicaId = clinica.Id, Name = "Fora", IsActive = true };
            db.Patients.AddRange(patientInside, patientOutside);
            await db.SaveChangesAsync();
            patientInside.CreatedAt = insidePeriod;
            patientOutside.CreatedAt = outsidePeriod;
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance3@a.local");

        var response = await client.GetAsync("/api/financial/balance?startDate=2026-03-01&endDate=2026-03-31");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, balance!.Patients.NewInPeriod);
        Assert.Equal(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), balance.Period.StartDate);
        Assert.Equal(new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc), balance.Period.EndDate);
    }

    [Fact]
    public async Task GetBalance_UserFromClinicA_DoesNotReturnClinicBData()
    {
        await using var app = new MultiClinicaFactory();
        var (clinicaA, _, _) = await SeedClinicaAsync(app, "Clinica A Isolada", "admin.balance4a@a.local");
        var (clinicaB, _, _) = await SeedClinicaAsync(app, "Clinica B Isolada", "admin.balance4b@a.local");

        await app.SeedAsync(async db =>
        {
            db.Patients.Add(new Patient { ClinicaId = clinicaA.Id, Name = "Paciente A", IsActive = true });
            db.Patients.Add(new Patient { ClinicaId = clinicaB.Id, Name = "Paciente B1", IsActive = true });
            db.Patients.Add(new Patient { ClinicaId = clinicaB.Id, Name = "Paciente B2", IsActive = true });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance4a@a.local");

        var response = await client.GetAsync("/api/financial/balance");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, balance!.Patients.Active);
    }

    [Fact]
    public async Task GetBalance_WithPaidPayments_ReturnsAppointmentIncome()
    {
        await using var app = new MultiClinicaFactory();
        var (clinica, admin, plan) = await SeedClinicaAsync(app, "Clinica Pagamentos", "admin.balance5@a.local");
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        await app.SeedAsync(async db =>
        {
            var patient = new Patient { ClinicaId = clinica.Id, Name = "Paciente", IsActive = true };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            db.Payments.AddRange(
                new Payment
                {
                    ClinicaId = clinica.Id, PatientId = patient.Id, UserId = admin.Id, PlanId = plan.Id,
                    ReferenceMonth = monthStart.ToString("MM-yyyy"), Amount = 150, PaymentMethod = "Pix",
                    Status = PaymentStatus.Paid, PaidAt = monthStart.AddDays(1)
                },
                new Payment
                {
                    ClinicaId = clinica.Id, PatientId = patient.Id, UserId = admin.Id, PlanId = plan.Id,
                    ReferenceMonth = monthStart.ToString("MM-yyyy"), Amount = 999, PaymentMethod = "Pix",
                    Status = PaymentStatus.Pending
                });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance5@a.local");

        var response = await client.GetAsync("/api/financial/balance");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(150, balance!.Money.AppointmentIncome);
        Assert.Equal(1, balance.Money.PaidAppointmentCount);
    }

    [Fact]
    public async Task GetBalance_WithProductSales_ReturnsProductSalesIncome()
    {
        await using var app = new MultiClinicaFactory();
        var (clinica, admin, _) = await SeedClinicaAsync(app, "Clinica Vendas", "admin.balance6@a.local");

        int produtoId = 0;
        await app.SeedAsync(async db =>
        {
            var produto = new Produto
            {
                ClinicaId = clinica.Id, Nome = "Produto Venda", ValorCompra = 10, ValorVenda = 40,
                QuantidadeAtual = 5, QuantidadeMinima = 1
            };
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();
            produtoId = produto.Id;
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance6@a.local");

        var saleResponse = await client.PostAsJsonAsync("/api/stock/movements/sale",
            new { ProductId = produtoId, Quantity = 3 });
        Assert.Equal(HttpStatusCode.Created, saleResponse.StatusCode);

        var response = await client.GetAsync("/api/financial/balance");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(120, balance!.Money.ProductSalesIncome);
        Assert.Equal(1, balance.Money.ProductSaleCount);
    }

    [Fact]
    public async Task GetBalance_WithStockCosts_ReturnsOutcome()
    {
        await using var app = new MultiClinicaFactory();
        var (clinica, admin, _) = await SeedClinicaAsync(app, "Clinica Custos", "admin.balance7@a.local");
        var now = DateTime.UtcNow;

        await app.SeedAsync(async db =>
        {
            var produto = new Produto
            {
                ClinicaId = clinica.Id, Nome = "Produto Custo", ValorCompra = 10, ValorVenda = 20,
                QuantidadeAtual = 10, QuantidadeMinima = 1
            };
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();

            db.MovimentacoesEstoque.AddRange(
                new MovimentacaoEstoque
                {
                    ClinicaId = clinica.Id, ProdutoId = produto.Id, Tipo = TipoMovimentacaoEstoque.Compra,
                    Quantidade = 5, QuantidadeAnterior = 5, QuantidadeAtual = 10, UnitValue = 10, TotalValue = 50,
                    UsuarioId = admin.Id, CreatedAt = now
                },
                new MovimentacaoEstoque
                {
                    ClinicaId = clinica.Id, ProdutoId = produto.Id, Tipo = TipoMovimentacaoEstoque.Perda,
                    Quantidade = 1, QuantidadeAnterior = 10, QuantidadeAtual = 9, UnitValue = 10, TotalValue = 10,
                    UsuarioId = admin.Id, CreatedAt = now
                });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance7@a.local");

        var response = await client.GetAsync("/api/financial/balance");
        var balance = await response.Content.ReadFromJsonAsync<FinancialBalanceDto>();

        Assert.Equal(50, balance!.Money.ProductPurchaseCost);
        Assert.Equal(10, balance.Money.ProductLossCost);
        Assert.Equal(60, balance.Money.TotalOutcome);
        Assert.Equal(-60, balance.Money.EstimatedProfit);
    }

    [Fact]
    public async Task GetBalance_DoesNotDependOnHeavyFinancialModule()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicaAsync(app, "Clinica Sem Financeiro Pesado", "admin.balance8@a.local");

        using var client = app.CreateClient();
        await LoginAsync(client, "admin.balance8@a.local");

        var response = await client.GetAsync("/api/financial/balance");
        var json = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(json).RootElement;

        string[] forbiddenKeys = ["contasPagar", "contasReceber", "caixa", "auditoria", "categoriasFinanceiras", "contasFinanceiras", "formasPagamento", "recebimentos"];
        var allKeys = CollectKeys(root).ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var forbidden in forbiddenKeys)
            Assert.DoesNotContain(forbidden, allKeys, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> CollectKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                yield return property.Name;
                foreach (var nested in CollectKeys(property.Value))
                    yield return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                foreach (var nested in CollectKeys(item))
                    yield return nested;
        }
    }
}
