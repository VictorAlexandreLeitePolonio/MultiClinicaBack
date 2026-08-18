using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ClinicPublicProfileTests
{
    private const string AdminPassword = "secret123";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record CategoryDto(int Id, string Name, string Slug);
    private sealed record HourDto(int Id, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
    private sealed record MediaDto(int Id, ClinicMediaType Type, int SortOrder, string Url);

    // ── Seeding ──────────────────────────────────────────────────────────────

    private sealed class ClinicCtx { public int ClinicId; public string AdminEmail = ""; }

    private static async Task<ClinicCtx> SeedClinicAsync(MultiClinicaFactory app, string suffix,
        bool isPublic = false, bool isActive = true, string? slug = null)
    {
        var ctx = new ClinicCtx { AdminEmail = $"admin-{suffix}@test.local" };
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = $"Clinica {suffix}", NomeFantasia = $"Clinica {suffix}", NomeResponsavel = "Victor",
                Email = $"clinic-{suffix}@test.local", Cidade = "São Paulo", Estado = "SP",
                IsActive = isActive, IsPublic = isPublic, PublicSlug = slug
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            ctx.ClinicId = clinic.Id;

            db.Users.Add(new User
            {
                ClinicaId = clinic.Id, Name = "Admin", Email = ctx.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword), Role = UserRole.Administrador
            });
            await db.SaveChangesAsync();
        });
        return ctx;
    }

    private static async Task<List<int>> SeedCategoriesAsync(MultiClinicaFactory app)
    {
        var ids = new List<int>();
        await app.SeedAsync(async db =>
        {
            var c1 = new ClinicCategory { Name = "Fisioterapia", Slug = "fisioterapia", IsActive = true };
            var c2 = new ClinicCategory { Name = "Pilates", Slug = "pilates", IsActive = true };
            db.ClinicCategories.AddRange(c1, c2);
            await db.SaveChangesAsync();
            ids.Add(c1.Id);
            ids.Add(c2.Id);
        });
        return ids;
    }

    private static async Task<HttpClient> LoginAdminAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = AdminPassword })).EnsureSuccessStatusCode();
        return client;
    }

    private static MultipartFormDataContent ImageUpload(string contentType, string fileName, string type = "Gallery")
    {
        var content = new MultipartFormDataContent();
        var bytes = new ByteArrayContent([0x1, 0x2, 0x3, 0x4]);
        bytes.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(bytes, "file", fileName);
        content.Add(new StringContent(type), "type");
        return content;
    }

    // ── slug ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Slug_must_be_unique()
    {
        await using var app = new MultiClinicaFactory();
        var a = await SeedClinicAsync(app, "1", slug: "clinica-existente");
        var b = await SeedClinicAsync(app, "2");

        using var admin = await LoginAdminAsync(app, b.AdminEmail);
        var response = await admin.PutAsJsonAsync("/api/clinic/settings", new { publicSlug = "clinica-existente" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── visibilidade pública ──────────────────────────────────────────────────

    [Fact]
    public async Task Private_clinic_is_not_public()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicAsync(app, "1", isPublic: false, slug: "privada");

        using var client = app.CreateClient();
        var response = await client.GetAsync("/api/public/clinics/privada");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_clinic_is_not_public()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicAsync(app, "1", isPublic: true, isActive: false, slug: "inativa");

        using var client = app.CreateClient();
        var response = await client.GetAsync("/api/public/clinics/inativa");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Public_clinic_is_returned_without_financial_or_admin_data()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Publica", NomeFantasia = "Publica", NomeResponsavel = "Victor",
                Cnpj = "12345678000199", ValorMensalidade = 999m, Email = "pub@test.local",
                IsPublic = true, IsActive = true, PublicSlug = "publica", AcceptsAppointmentRequests = true
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var response = await client.GetAsync("/api/public/clinics/publica");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();

        foreach (var forbidden in new[] { "valorMensalidade", "cnpj", "cobranca", "billing", "isBlockedByBilling", "diaVencimento" })
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
    }

    // ── categorias ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Clinic_can_have_multiple_categories()
    {
        await using var app = new MultiClinicaFactory();
        var ctx = await SeedClinicAsync(app, "1");
        var ids = await SeedCategoriesAsync(app);

        using var admin = await LoginAdminAsync(app, ctx.AdminEmail);
        var set = await admin.PutAsJsonAsync("/api/clinic/categories", new { categoryIds = ids });
        set.EnsureSuccessStatusCode();

        var list = await admin.GetFromJsonAsync<List<CategoryDto>>("/api/clinic/categories", Json);
        Assert.Equal(2, list!.Count);
    }

    // ── horários ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Multiple_business_hours_per_day_are_allowed()
    {
        await using var app = new MultiClinicaFactory();
        var ctx = await SeedClinicAsync(app, "1");
        using var admin = await LoginAdminAsync(app, ctx.AdminEmail);

        var morning = await admin.PostAsJsonAsync("/api/clinic/business-hours",
            new { dayOfWeek = "Monday", startTime = "08:00:00", endTime = "12:00:00" });
        var afternoon = await admin.PostAsJsonAsync("/api/clinic/business-hours",
            new { dayOfWeek = "Monday", startTime = "13:00:00", endTime = "18:00:00" });

        morning.EnsureSuccessStatusCode();
        afternoon.EnsureSuccessStatusCode();

        var list = await admin.GetFromJsonAsync<List<HourDto>>("/api/clinic/business-hours", Json);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task Overlapping_business_hours_are_rejected()
    {
        await using var app = new MultiClinicaFactory();
        var ctx = await SeedClinicAsync(app, "1");
        using var admin = await LoginAdminAsync(app, ctx.AdminEmail);

        (await admin.PostAsJsonAsync("/api/clinic/business-hours",
            new { dayOfWeek = "Tuesday", startTime = "08:00:00", endTime = "12:00:00" })).EnsureSuccessStatusCode();

        var overlap = await admin.PostAsJsonAsync("/api/clinic/business-hours",
            new { dayOfWeek = "Tuesday", startTime = "11:00:00", endTime = "14:00:00" });
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
    }

    [Fact]
    public async Task Invalid_time_range_is_rejected()
    {
        await using var app = new MultiClinicaFactory();
        var ctx = await SeedClinicAsync(app, "1");
        using var admin = await LoginAdminAsync(app, ctx.AdminEmail);

        var response = await admin.PostAsJsonAsync("/api/clinic/business-hours",
            new { dayOfWeek = "Wednesday", startTime = "18:00:00", endTime = "08:00:00" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── mídia ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Valid_media_upload_succeeds()
    {
        await using var app = new MultiClinicaFactory();
        var ctx = await SeedClinicAsync(app, "1");
        using var admin = await LoginAdminAsync(app, ctx.AdminEmail);

        var response = await admin.PostAsync("/api/clinic/media", ImageUpload("image/png", "cover.png", "Cover"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<MediaDto>(Json);
        Assert.Equal(ClinicMediaType.Cover, dto!.Type);
        Assert.False(string.IsNullOrWhiteSpace(dto.Url));
    }

    [Fact]
    public async Task Invalid_content_type_is_rejected()
    {
        await using var app = new MultiClinicaFactory();
        var ctx = await SeedClinicAsync(app, "1");
        using var admin = await LoginAdminAsync(app, ctx.AdminEmail);

        var response = await admin.PostAsync("/api/clinic/media", ImageUpload("application/pdf", "file.pdf"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Media_is_isolated_between_clinics()
    {
        await using var app = new MultiClinicaFactory();
        var a = await SeedClinicAsync(app, "1");
        var b = await SeedClinicAsync(app, "2");

        using var adminA = await LoginAdminAsync(app, a.AdminEmail);
        var upload = await adminA.PostAsync("/api/clinic/media", ImageUpload("image/jpeg", "g.jpg"));
        var media = await upload.Content.ReadFromJsonAsync<MediaDto>(Json);

        using var adminB = await LoginAdminAsync(app, b.AdminEmail);
        var listB = await adminB.GetFromJsonAsync<List<MediaDto>>("/api/clinic/media", Json);
        Assert.Empty(listB!);

        var deleteFromB = await adminB.DeleteAsync($"/api/clinic/media/{media!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteFromB.StatusCode);
    }

    [Fact]
    public async Task Public_profile_exposes_gallery_and_categories()
    {
        await using var app = new MultiClinicaFactory();
        var ctx = await SeedClinicAsync(app, "1", isPublic: true, slug: "clinica-completa");
        var ids = await SeedCategoriesAsync(app);

        using var admin = await LoginAdminAsync(app, ctx.AdminEmail);
        (await admin.PutAsJsonAsync("/api/clinic/categories", new { categoryIds = ids })).EnsureSuccessStatusCode();
        (await admin.PostAsync("/api/clinic/media", ImageUpload("image/png", "g1.png", "Gallery"))).EnsureSuccessStatusCode();

        using var client = app.CreateClient();
        var raw = await (await client.GetAsync("/api/public/clinics/clinica-completa")).Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("categories").GetArrayLength());
        Assert.Equal(1, root.GetProperty("gallery").GetArrayLength());
    }
}
