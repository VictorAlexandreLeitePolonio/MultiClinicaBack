using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Stock;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class StockMovementSaleTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task<(int ProdutoId, string AdminEmail)> SeedAsync(MultiClinicaFactory app)
    {
        var produtoId = 0;
        const string email = "admin.sale@a.local";

        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica Venda", NomeResponsavel = "Victor" };
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

            var produto = new Produto
            {
                ClinicaId = clinica.Id,
                Nome = "Produto Venda",
                ValorCompra = 10,
                ValorVenda = 30,
                QuantidadeAtual = 5,
                QuantidadeMinima = 1
            };
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();
            produtoId = produto.Id;
        });

        return (produtoId, email);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RegisterSale_ProductWithStock_ReducesQuantityAndRecordsValue()
    {
        await using var app = new MultiClinicaFactory();
        var (produtoId, email) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/stock/movements/sale",
            new CreateStockMovementRequest { ProductId = produtoId, Quantity = 2, Note = "Venda balcão" });
        var movement = await response.Content.ReadFromJsonAsync<StockMovementResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Venda", movement!.Type);
        Assert.Equal(2, movement.Quantity);
        Assert.Equal(3, movement.CurrentQuantity);
        Assert.Equal(30, movement.UnitValue);
        Assert.Equal(60, movement.TotalValue);
    }

    [Fact]
    public async Task RegisterSale_QuantityGreaterThanStock_ReturnsBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (produtoId, email) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/stock/movements/sale",
            new CreateStockMovementRequest { ProductId = produtoId, Quantity = 999 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSale_InvalidQuantity_ReturnsBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        var (produtoId, email) = await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/stock/movements/sale",
            new CreateStockMovementRequest { ProductId = produtoId, Quantity = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
