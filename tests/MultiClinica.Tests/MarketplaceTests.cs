using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class MarketplaceTests
{
    private const string Password = "senha-super-forte";

    private sealed record ClinicCard(int Id, string? DisplayName, int LikeCount, bool LikedByMe);
    private sealed record ClinicPage(IReadOnlyList<ClinicCard> Data, int TotalCount, int Page, int PageSize);
    private sealed record Category(int Id, string Name, string Slug);
    private sealed record ClinicDetails(
        int Id,
        string? CoverUrl,
        IReadOnlyList<string> Gallery,
        bool LikedByMe,
        bool IsLinked);

    [Fact]
    public async Task Patient_lists_only_public_active_clinics_with_private_like_state()
    {
        await using var app = new MultiClinicaFactory();
        var publicClinicId = 0;
        await app.SeedAsync(async db =>
        {
            var account = new PatientAccount
            {
                Name = "Paciente",
                Email = "marketplace-list@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Status = PatientAccountStatus.Active,
                ActivatedAt = DateTime.UtcNow,
            };
            var publicClinic = new Clinica
            {
                Nome = "Clínica Pública",
                NomeFantasia = "Clínica Pública",
                NomeResponsavel = "Responsável",
                IsPublic = true,
                LikeCount = 8,
            };
            var privateClinic = new Clinica
            {
                Nome = "Clínica Privada",
                NomeFantasia = "Clínica Privada",
                NomeResponsavel = "Responsável",
                IsPublic = false,
            };
            db.AddRange(account, publicClinic, privateClinic);
            await db.SaveChangesAsync();
            publicClinicId = publicClinic.Id;

            db.ClinicLikes.Add(new ClinicLike
            {
                PatientAccountId = account.Id,
                ClinicaId = publicClinic.Id,
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new
        {
            email = "marketplace-list@test.local",
            password = Password,
        })).EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/patient/marketplace/clinics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ClinicPage>();
        var clinic = Assert.Single(page!.Data);
        Assert.Equal(publicClinicId, clinic.Id);
        Assert.True(clinic.LikedByMe);
        Assert.Equal(8, clinic.LikeCount);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(12, page.PageSize);
    }

    [Fact]
    public async Task Category_catalog_returns_only_active_non_deleted_items_sorted_by_name()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(ActiveAccount("marketplace-categories@test.local"));
            db.ClinicCategories.AddRange(
                new ClinicCategory { Name = "Zeta", Slug = "zeta" },
                new ClinicCategory { Name = "Alpha", Slug = "alpha" },
                new ClinicCategory { Name = "Inativa", Slug = "inativa", IsActive = false },
                new ClinicCategory { Name = "Excluída", Slug = "excluida", IsDeleted = true });
            await db.SaveChangesAsync();
        });
        using var client = await LoginAsync(app, "marketplace-categories@test.local");

        var categories = await client.GetFromJsonAsync<List<Category>>(
            "/api/patient/marketplace/categories");

        Assert.Equal(["Alpha", "Zeta"], categories!.Select(category => category.Name));
    }

    [Fact]
    public async Task List_applies_repeated_category_and_location_filters_before_pagination()
    {
        await using var app = new MultiClinicaFactory();
        var matchingClinicId = 0;
        var categoryId = 0;
        await app.SeedAsync(async db =>
        {
            var account = ActiveAccount("marketplace-filters@test.local");
            var category = new ClinicCategory { Name = "Psicologia", Slug = "psicologia" };
            var matching = PublicClinic("Clínica Aurora", "Itapetininga", "SP", likes: 4);
            matching.AcceptsAppointmentRequests = true;
            matching.Categories.Add(category);
            var other = PublicClinic("Clínica Beta", "Sorocaba", "SP", likes: 20);
            db.AddRange(account, matching, other);
            await db.SaveChangesAsync();
            matchingClinicId = matching.Id;
            categoryId = category.Id;
            db.ClinicLikes.Add(new ClinicLike
            {
                PatientAccountId = account.Id,
                ClinicaId = matching.Id,
            });
            await db.SaveChangesAsync();
        });
        using var client = await LoginAsync(app, "marketplace-filters@test.local");

        var page = await client.GetFromJsonAsync<ClinicPage>(
            $"/api/patient/marketplace/clinics?categoryIds={categoryId}&categoryIds=999&city=itapetininga&state=sp&acceptsAppointmentRequests=true&likedOnly=true&page=1&pageSize=1");

        var clinic = Assert.Single(page!.Data);
        Assert.Equal(matchingClinicId, clinic.Id);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.PageSize);
    }

    [Fact]
    public async Task Detail_returns_temporary_media_urls_and_patient_relationship_state()
    {
        await using var app = new MultiClinicaFactory();
        var clinicId = 0;
        await app.SeedAsync(async db =>
        {
            var account = ActiveAccount("marketplace-detail@test.local");
            var clinic = PublicClinic("Clínica Detalhe", "Itapetininga", "SP", likes: 1);
            db.AddRange(account, clinic);
            await db.SaveChangesAsync();
            clinicId = clinic.Id;
            db.ClinicMedia.AddRange(
                new ClinicMedia { ClinicaId = clinic.Id, Type = ClinicMediaType.Cover, ObjectKey = "cover.jpg" },
                new ClinicMedia { ClinicaId = clinic.Id, Type = ClinicMediaType.Gallery, ObjectKey = "gallery.jpg" });
            db.ClinicLikes.Add(new ClinicLike { PatientAccountId = account.Id, ClinicaId = clinic.Id });
            db.Patients.Add(new Patient
            {
                ClinicaId = clinic.Id,
                PatientAccountId = account.Id,
                Name = "Paciente",
            });
            await db.SaveChangesAsync();
        });
        using var client = await LoginAsync(app, "marketplace-detail@test.local");

        var detail = await client.GetFromJsonAsync<ClinicDetails>(
            $"/api/patient/marketplace/clinics/{clinicId}");

        Assert.Equal(clinicId, detail!.Id);
        Assert.Equal("https://storage.test/cover.jpg", detail.CoverUrl);
        Assert.Equal(["https://storage.test/gallery.jpg"], detail.Gallery);
        Assert.True(detail.LikedByMe);
        Assert.True(detail.IsLinked);
    }

    [Fact]
    public async Task Detail_hides_private_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var clinicId = 0;
        await app.SeedAsync(async db =>
        {
            db.PatientAccounts.Add(ActiveAccount("marketplace-private@test.local"));
            var clinic = new Clinica
            {
                Nome = "Privada",
                NomeResponsavel = "Responsável",
                IsPublic = false,
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            clinicId = clinic.Id;
        });
        using var client = await LoginAsync(app, "marketplace-private@test.local");

        var response = await client.GetAsync($"/api/patient/marketplace/clinics/{clinicId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static PatientAccount ActiveAccount(string email) => new()
    {
        Name = "Paciente",
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        Status = PatientAccountStatus.Active,
        ActivatedAt = DateTime.UtcNow,
    };

    private static Clinica PublicClinic(string name, string city, string state, int likes) => new()
    {
        Nome = name,
        NomeFantasia = name,
        NomeResponsavel = "Responsável",
        Cidade = city,
        Estado = state,
        IsPublic = true,
        LikeCount = likes,
    };

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new { email, password = Password }))
            .EnsureSuccessStatusCode();
        return client;
    }
}
