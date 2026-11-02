using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PublicHubTests
{
    private const string Password = "senha-super-forte";

    private sealed record ClinicCard(int Id, string? Slug, string? DisplayName, int LikeCount, bool LikedByMe);
    private sealed record ClinicPage(IReadOnlyList<ClinicCard> Data, int TotalCount, int Page, int PageSize);
    private sealed record Category(int Id, string Name, string Slug);

    [Fact]
    public async Task Search_without_login_returns_only_public_approved_clinics_with_slug()
    {
        await using var app = new MultiClinicaFactory();
        var visibleClinicId = 0;
        await app.SeedAsync(async db =>
        {
            var visible = PublicClinic("Clínica Visível", "Itapetininga", "SP", "visivel");
            var withoutSlug = PublicClinic("Clínica Sem Slug", "Sorocaba", "SP", null);
            var notApproved = PublicClinic("Clínica Não Aprovada", "Campinas", "SP", "nao-aprovada");
            notApproved.IsHubApproved = false;
            var privateClinic = PublicClinic("Clínica Privada", "São Paulo", "SP", "privada");
            privateClinic.IsPublic = false;

            db.Clinicas.AddRange(visible, withoutSlug, notApproved, privateClinic);
            await db.SaveChangesAsync();
            visibleClinicId = visible.Id;
        });

        using var client = app.CreateClient();
        var response = await client.GetAsync("/api/public/clinics/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ClinicPage>();
        var clinic = Assert.Single(page!.Data);
        Assert.Equal(visibleClinicId, clinic.Id);
        Assert.Equal("visivel", clinic.Slug);
        Assert.False(clinic.LikedByMe);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Search_filters_by_city_and_category()
    {
        await using var app = new MultiClinicaFactory();
        var auroraId = 0;
        var betaId = 0;
        var categoryId = 0;
        await app.SeedAsync(async db =>
        {
            var category = new ClinicCategory { Name = "Psicologia", Slug = "psicologia" };
            var aurora = PublicClinic("Clínica Aurora", "Itapetininga", "SP", "aurora");
            aurora.Categories.Add(category);
            var beta = PublicClinic("Clínica Beta", "Sorocaba", "SP", "beta");

            db.Clinicas.AddRange(aurora, beta);
            await db.SaveChangesAsync();
            auroraId = aurora.Id;
            betaId = beta.Id;
            categoryId = category.Id;
        });

        using var client = app.CreateClient();

        var byCity = await client.GetFromJsonAsync<ClinicPage>(
            "/api/public/clinics/search?city=itapetininga");
        Assert.Equal(auroraId, Assert.Single(byCity!.Data).Id);

        var byCategory = await client.GetFromJsonAsync<ClinicPage>(
            $"/api/public/clinics/search?categoryIds={categoryId}");
        Assert.Equal(auroraId, Assert.Single(byCategory!.Data).Id);

        var byOtherCity = await client.GetFromJsonAsync<ClinicPage>(
            "/api/public/clinics/search?city=sorocaba");
        Assert.Equal(betaId, Assert.Single(byOtherCity!.Data).Id);
    }

    [Fact]
    public async Task Search_ignores_liked_only_without_login()
    {
        await using var app = new MultiClinicaFactory();
        var clinicId = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = PublicClinic("Clínica Pública", "Sorocaba", "SP", "publica");
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            clinicId = clinic.Id;
        });

        using var client = app.CreateClient();
        var response = await client.GetAsync("/api/public/clinics/search?likedOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ClinicPage>();
        Assert.Equal(clinicId, Assert.Single(page!.Data).Id);
    }

    [Fact]
    public async Task Search_with_staff_cookie_does_not_leak_likes()
    {
        await using var app = new MultiClinicaFactory();
        const string email = "staff-hub@test.local";
        var clinicId = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = PublicClinic("Clínica Staff", "Sorocaba", "SP", "staff");
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            clinicId = clinic.Id;

            var staff = new User
            {
                ClinicaId = clinic.Id,
                Name = "Funcionário",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Recepcao,
            };
            db.Users.Add(staff);
            await db.SaveChangesAsync();

            // Like "do funcionário": numa rota anônima o NameIdentifier não pode virar paciente.
            db.ClinicLikes.Add(new ClinicLike
            {
                PatientAccountId = staff.Id,
                ClinicaId = clinic.Id,
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password }))
            .EnsureSuccessStatusCode();

        var page = await client.GetFromJsonAsync<ClinicPage>("/api/public/clinics/search");

        Assert.Contains(page!.Data, clinic => clinic.Id == clinicId);
        Assert.All(page.Data, clinic => Assert.False(clinic.LikedByMe));
    }

    [Fact]
    public async Task Categories_without_login_returns_active_categories()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.ClinicCategories.AddRange(
                new ClinicCategory { Name = "Ativa", Slug = "ativa" },
                new ClinicCategory { Name = "Inativa", Slug = "inativa", IsActive = false },
                new ClinicCategory { Name = "Excluída", Slug = "excluida", IsDeleted = true });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var response = await client.GetAsync("/api/public/clinics/categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<Category>>();
        Assert.Contains(categories!, category => category.Name == "Ativa");
        Assert.DoesNotContain(categories!, category => category.Name is "Inativa" or "Excluída");
    }

    private static Clinica PublicClinic(string name, string city, string state, string? slug) => new()
    {
        Nome = name,
        NomeFantasia = name,
        NomeResponsavel = "Responsável",
        Cidade = city,
        Estado = state,
        IsPublic = true,
        IsHubApproved = true,
        PublicSlug = slug,
    };
}
