using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PaymentContractTests
{
    [Fact]
    public async Task CreatePaidPayment_PreservesStatusAndCivilDates()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedClinicAsync(app, "admin.payment-contract@test.local");
        using var client = await LoginAsync(app, "admin.payment-contract@test.local");

        var response = await client.PostAsJsonAsync("/api/payments", new
        {
            responsavelId = seed.AdminId,
            patientId = seed.PatientId,
            planId = seed.PlanId,
            referenceMonth = "2026-09-20",
            paymentMethod = "Pix",
            status = "Paid",
            paidAt = "2026-09-04",
            paymentDate = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paymentId = body.GetProperty("id").GetInt32();
        Assert.Equal("Paid", body.GetProperty("status").GetString());
        Assert.Equal("2026-09-20", body.GetProperty("referenceMonth").GetString());
        Assert.Equal("2026-09-04", body.GetProperty("paidAt").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("paymentDate").ValueKind);

        var read = await client.GetFromJsonAsync<JsonElement>($"/api/payments/{paymentId}");
        Assert.Equal("Paid", read.GetProperty("status").GetString());
        Assert.Equal("2026-09-04", read.GetProperty("paidAt").GetString());

        var duplicate = await client.PostAsJsonAsync("/api/payments", new
        {
            responsavelId = seed.AdminId,
            patientId = seed.PatientId,
            planId = seed.PlanId,
            referenceMonth = "2026-09-01",
            paymentMethod = "Pix",
            status = "Pending",
            paidAt = (string?)null,
            paymentDate = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task PaymentReferenceFilter_UsesTheWholeMonth_AndUpdateIgnoresItself()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedClinicAsync(app, "admin.payment-filter@test.local");
        using var client = await LoginAsync(app, "admin.payment-filter@test.local");

        var create = await client.PostAsJsonAsync("/api/payments", new
        {
            responsavelId = seed.AdminId,
            patientId = seed.PatientId,
            planId = seed.PlanId,
            referenceMonth = "2026-09-20",
            paymentMethod = "Pix",
            status = "Pending",
            paymentDate = (string?)null,
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var paymentId = created.GetProperty("id").GetInt32();

        var update = await client.PutAsJsonAsync($"/api/payments/{paymentId}", new
        {
            patientId = seed.PatientId,
            planId = seed.PlanId,
            referenceMonth = "2026-09-25",
            paymentMethod = "Pix",
            status = "Pending",
            paidAt = (string?)null,
            paymentDate = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        foreach (var date in new[] { "2026-09-01", "2026-09-20", "2026-09-25" })
        {
            var list = await client.GetFromJsonAsync<JsonElement>($"/api/payments?referenceMonth={date}");
            Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        }

        var invalid = await client.GetAsync("/api/payments?referenceMonth=09-2026");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static async Task<(int AdminId, int PatientId, int PlanId)> SeedClinicAsync(
        MultiClinicaFactory app, string adminEmail)
    {
        var ids = (AdminId: 0, PatientId: 0, PlanId: 0);
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Payment Contract", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador,
            };
            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente", IsActive = true };
            db.Users.Add(admin);
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var sessionType = new SessionType { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            db.SessionTypes.Add(sessionType);
            await db.SaveChangesAsync();

            var plan = new Plans
            {
                ClinicaId = clinic.Id,
                Name = "Mensal",
                Valor = 150,
                TipoPlano = TipoPlano.Mensal,
                TipoSessaoId = sessionType.Id,
            };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
            ids = (admin.Id, patient.Id, plan.Id);
        });
        return ids;
    }

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = "secret123",
        });
        response.EnsureSuccessStatusCode();
        return client;
    }
}
