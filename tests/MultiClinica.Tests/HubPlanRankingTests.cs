using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class HubPlanRankingTests
{
    private const string Password = "senha-super-forte";
    private const string PatientEmail = "hub-patient@test.local";
    private const string SuperAdminEmail = "hub-root@test.local";

    private static async Task<int> SeedClinicAsync(
        MultiClinicaFactory app,
        string name,
        ClinicPlan plan,
        bool founder = false,
        bool isPublic = true,
        bool approved = true,
        int likes = 0,
        string? slug = null)
    {
        var id = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = name,
                NomeFantasia = name,
                NomeResponsavel = "Responsável",
                Cidade = "São Paulo",
                Estado = "SP",
                IsPublic = isPublic,
                IsHubApproved = approved,
                IsFounder = founder,
                Plan = plan,
                LikeCount = likes,
                PublicSlug = slug,
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            id = clinic.Id;
        });
        return id;
    }

    private static async Task<HttpClient> LoginPatientAsync(MultiClinicaFactory app)
    {
        await app.SeedAsync(async db =>
        {
            if (await db.PatientAccounts.AnyAsync(account => account.Email == PatientEmail))
                return;
            db.PatientAccounts.Add(new PatientAccount
            {
                Name = "Paciente",
                Email = PatientEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Status = PatientAccountStatus.Active,
                ActivatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        });
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login",
            new { email = PatientEmail, password = Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<HttpClient> LoginSuperAdminAsync(MultiClinicaFactory app)
    {
        await app.SeedAsync(async db =>
        {
            if (await db.Users.AnyAsync(user => user.Email == SuperAdminEmail))
                return;
            var clinic = new Clinica { Nome = "Admin Interno", NomeResponsavel = "Root" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Root",
                Email = SuperAdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.SuperAdmin,
            });
            await db.SaveChangesAsync();
        });
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login",
            new { email = SuperAdminEmail, password = Password })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<List<int>> MarketplaceOrderAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/patient/marketplace/clinics?pageSize=50");
        response.EnsureSuccessStatusCode();
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        return root.GetProperty("data").EnumerateArray()
            .Select(card => card.GetProperty("id").GetInt32())
            .ToList();
    }

    [Fact]
    public async Task Default_listing_orders_premium_then_pro_then_free_even_with_more_likes_on_free()
    {
        await using var app = new MultiClinicaFactory();
        var freeId = await SeedClinicAsync(app, "Free", ClinicPlan.Free, likes: 100, slug: "free");
        var proId = await SeedClinicAsync(app, "Pro", ClinicPlan.Pro, likes: 50, slug: "pro");
        var premiumId = await SeedClinicAsync(app, "Premium", ClinicPlan.Premium, likes: 1, slug: "premium");
        using var client = await LoginPatientAsync(app);

        var order = await MarketplaceOrderAsync(client);

        Assert.Equal([premiumId, proId, freeId], order);
    }

    [Fact]
    public async Task Unapproved_public_clinic_is_hidden_from_marketplace_and_public_profile()
    {
        await using var app = new MultiClinicaFactory();
        var clinicId = await SeedClinicAsync(app, "Pendente", ClinicPlan.Pro, approved: false, slug: "pendente");
        using var patient = await LoginPatientAsync(app);

        var order = await MarketplaceOrderAsync(patient);
        Assert.DoesNotContain(clinicId, order);

        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await anonymous.GetAsync("/api/public/clinics/pendente")).StatusCode);
        var showcase = JsonDocument.Parse(
            await (await anonymous.GetAsync("/api/public/clinics")).Content.ReadAsStringAsync()).RootElement;
        Assert.DoesNotContain("pendente",
            showcase.EnumerateArray().Select(clinic => clinic.GetProperty("slug").GetString()));
    }

    [Fact]
    public async Task Premium_order_is_stable_within_the_same_day()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicAsync(app, "Premium A", ClinicPlan.Premium, likes: 1, slug: "premium-a");
        await SeedClinicAsync(app, "Premium B", ClinicPlan.Premium, likes: 2, slug: "premium-b");
        using var client = await LoginPatientAsync(app);

        var first = await MarketplaceOrderAsync(client);
        var second = await MarketplaceOrderAsync(client);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Daily_rotation_key_inverts_the_order_between_days()
    {
        static long Key(int clinicId, long day) => ((long)clinicId * 7919 + day * 104729) % 1000003;

        Assert.True(Key(1, 739000) < Key(2, 739000));
        Assert.True(Key(2, 739062) < Key(1, 739062));
    }

    [Fact]
    public async Task Cards_expose_founder_featured_and_no_badge()
    {
        await using var app = new MultiClinicaFactory();
        var founderId = await SeedClinicAsync(app, "Fundadora", ClinicPlan.Premium,
            founder: true, slug: "fundadora");
        var featuredId = await SeedClinicAsync(app, "Destaque", ClinicPlan.Premium, slug: "destaque");
        var proId = await SeedClinicAsync(app, "Pro", ClinicPlan.Pro, slug: "pro-badge");
        using var patient = await LoginPatientAsync(app);

        var response = await patient.GetAsync("/api/patient/marketplace/clinics?pageSize=50");
        response.EnsureSuccessStatusCode();
        var cards = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("data").EnumerateArray().ToList();
        string? Badge(int id) => cards.Single(card => card.GetProperty("id").GetInt32() == id)
            .GetProperty("badge").GetString();
        Assert.Equal("Founder", Badge(founderId));
        Assert.Equal("Featured", Badge(featuredId));
        Assert.Null(Badge(proId));

        using var anonymous = app.CreateClient();
        var showcase = JsonDocument.Parse(
            await (await anonymous.GetAsync("/api/public/clinics")).Content.ReadAsStringAsync()).RootElement;
        string? ShowcaseBadge(string slug) => showcase.EnumerateArray()
            .Single(clinic => clinic.GetProperty("slug").GetString() == slug)
            .GetProperty("badge").GetString();
        Assert.Equal("Founder", ShowcaseBadge("fundadora"));
        Assert.Equal("Featured", ShowcaseBadge("destaque"));
        Assert.Null(ShowcaseBadge("pro-badge"));
    }

    [Fact]
    public async Task SuperAdmin_approves_pending_clinic_and_rejects_with_reason()
    {
        await using var app = new MultiClinicaFactory();
        var pendingId = await SeedClinicAsync(app, "Pendente A", ClinicPlan.Free,
            approved: false, slug: "pendente-a");
        var rejectedId = await SeedClinicAsync(app, "Pendente B", ClinicPlan.Free,
            approved: false, slug: "pendente-b");
        using var admin = await LoginSuperAdminAsync(app);

        var list = JsonDocument.Parse(
            await (await admin.GetAsync("/api/superadmin/hub-approvals")).Content.ReadAsStringAsync()).RootElement;
        var pending = list.EnumerateArray().Single(item => item.GetProperty("clinicaId").GetInt32() == pendingId);
        Assert.Equal("Pendente A", pending.GetProperty("name").GetString());
        Assert.Equal("pendente-a", pending.GetProperty("publicSlug").GetString());
        Assert.Equal("São Paulo", pending.GetProperty("city").GetString());
        Assert.True(pending.GetProperty("updatedAt").ValueKind == JsonValueKind.String);

        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsJsonAsync($"/api/superadmin/clinicas/{pendingId}/hub-approval",
                new { approved = true })).StatusCode);
        using var patient = await LoginPatientAsync(app);
        Assert.Contains(pendingId, await MarketplaceOrderAsync(patient));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await admin.PostAsJsonAsync($"/api/superadmin/clinicas/{rejectedId}/hub-approval",
                new { approved = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.PostAsJsonAsync($"/api/superadmin/clinicas/{rejectedId}/hub-approval",
                new { approved = false, reason = "Perfil incompleto" })).StatusCode);
        await app.SeedAsync(async db =>
        {
            var rejected = await db.Clinicas.SingleAsync(clinic => clinic.Id == rejectedId);
            Assert.False(rejected.IsPublic);
            var history = await db.CommercialHistoryEvents
                .Where(item => item.ClinicaId == rejectedId).ToListAsync();
            Assert.Contains(history, item => item.Type == CommercialHistoryEventType.HubRejected
                && item.Description == "Perfil incompleto");
        });
    }

    [Fact]
    public async Task SuperAdmin_can_set_clinic_plan()
    {
        await using var app = new MultiClinicaFactory();
        var clinicId = await SeedClinicAsync(app, "Clínica Plano", ClinicPlan.Free, slug: "clinica-plano");
        using var admin = await LoginSuperAdminAsync(app);

        var response = await admin.PutAsJsonAsync($"/api/superadmin/clinicas/{clinicId}/plan",
            new { plan = "Pro", isFounder = false });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(item => item.Id == clinicId);
            Assert.Equal(ClinicPlan.Pro, clinic.Plan);
            Assert.False(clinic.IsFounder);
        });
    }
}
