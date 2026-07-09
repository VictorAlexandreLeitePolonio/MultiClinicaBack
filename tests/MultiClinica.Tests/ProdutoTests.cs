using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.Financial;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ProdutoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static async Task SeedAsync(MultiClinicaFactory app)
    {
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.AddRange(
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Admin",
                    Email = "admin.produto@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Administrador
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Recep",
                    Email = "recep.produto@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Recepcao
                },
                new User
                {
                    ClinicaId = clinica.Id,
                    Name = "Fisio",
                    Email = "fisio.produto@a.local",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                    Role = UserRole.Profissional
                });
            await db.SaveChangesAsync();
        });
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = email, Password = "secret123" });
        response.EnsureSuccessStatusCode();
    }

    private static CreateProdutoDto ValidDto() => new()
    {
        Nome = "Creme Hidratante",
        ValorCompra = 10,
        ValorVenda = 25,
        QuantidadeMinima = 5
    };

    [Fact]
    public async Task CriarProduto_AdminValido_Retorna201()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");

        var response = await client.PostAsJsonAsync("/api/produtos", ValidDto());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CriarProduto_NomeVazio_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var dto = ValidDto();
        dto.Nome = "";

        var response = await client.PostAsJsonAsync("/api/produtos", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarProduto_ValorCompraNegativo_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var dto = ValidDto();
        dto.ValorCompra = -1;

        var response = await client.PostAsJsonAsync("/api/produtos", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarProduto_ValorVendaNegativo_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var dto = ValidDto();
        dto.ValorVenda = -1;

        var response = await client.PostAsJsonAsync("/api/produtos", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CriarProduto_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "recep.produto@a.local");

        var response = await client.PostAsJsonAsync("/api/produtos", ValidDto());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetProdutos_Profissional_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "fisio.produto@a.local");

        var response = await client.GetAsync("/api/produtos");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CriarProduto_QuantidadeAtualComecaZero()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");

        var response = await client.PostAsJsonAsync("/api/produtos", ValidDto());
        var produto = await response.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        Assert.Equal(0, produto!.QuantidadeAtual);
    }

    [Fact]
    public async Task GetProduto_Recepcao_NaoRetornaValorCompra()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/produtos", ValidDto());
        var created = await createResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        await LoginAsync(client, "recep.produto@a.local");
        var response = await client.GetAsync($"/api/produtos/{created!.Id}");
        var produto = await response.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        Assert.Null(produto!.ValorCompra);
    }

    [Fact]
    public async Task GetProduto_Recepcao_RetornaValorVenda()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/produtos", ValidDto());
        var created = await createResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        await LoginAsync(client, "recep.produto@a.local");
        var response = await client.GetAsync($"/api/produtos/{created!.Id}");
        var produto = await response.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        Assert.Equal(25, produto!.ValorVenda);
    }

    [Fact]
    public async Task GetProduto_Admin_RetornaValorCompra()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/produtos", ValidDto());
        var created = await createResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        var response = await client.GetAsync($"/api/produtos/{created!.Id}");
        var produto = await response.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        Assert.Equal(10, produto!.ValorCompra);
    }

    [Fact]
    public async Task InativarProduto_Recepcao_Retorna403()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/produtos", ValidDto());
        var created = await createResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        await LoginAsync(client, "recep.produto@a.local");
        var response = await client.PostAsync($"/api/produtos/{created!.Id}/inativar", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InativarEReativarProduto_Admin_AlternaIsActive()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var createResponse = await client.PostAsJsonAsync("/api/produtos", ValidDto());
        var created = await createResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        var inativarResponse = await client.PostAsync($"/api/produtos/{created!.Id}/inativar", null);
        var inativado = await inativarResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        var reativarResponse = await client.PostAsync($"/api/produtos/{created.Id}/reativar", null);
        var reativado = await reativarResponse.Content.ReadFromJsonAsync<ProdutoResponseDto>(JsonOptions);

        Assert.False(inativado!.IsActive);
        Assert.True(reativado!.IsActive);
    }

    [Fact]
    public async Task CriarProduto_CategoriaProdutoInexistente_RetornaBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app);
        using var client = app.CreateClient();
        await LoginAsync(client, "admin.produto@a.local");
        var dto = ValidDto();
        dto.CategoriaProdutoId = 9999;

        var response = await client.PostAsJsonAsync("/api/produtos", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
