using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PaymentSummaryTests
{
    private const string Password = "secret123";

    [Fact]
    public async Task Summary_counts_every_payment_of_the_clinic_by_status_and_ignores_other_clinics()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app, "admin.summary@test.local");

        using var client = await LoginAsync(app, "admin.summary@test.local");

        // pageSize/page são ignorados: o resumo sempre considera o filtro inteiro.
        var summary = await client.GetFromJsonAsync<List<JsonElement>>(
            "/api/payments/summary?page=2&pageSize=1");

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Count);

        AssertSummary(summary[0], "Pending", 3, seed.PendingTotal);
        AssertSummary(summary[1], "Paid", 2, seed.PaidTotal);
        AssertSummary(summary[2], "Cancelled", 0, 0m);
    }

    [Fact]
    public async Task Summary_reference_month_covers_the_whole_month()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app, "admin.summary-month@test.local", seedPayments: false);

        await app.SeedAsync(async db =>
        {
            db.Payments.AddRange(
                NewPayment(seed, new DateOnly(2026, 8, 31), 50m, PaymentStatus.Paid),
                NewPayment(seed, new DateOnly(2026, 9, 1), 100m, PaymentStatus.Pending),
                NewPayment(seed, new DateOnly(2026, 9, 20), 200m, PaymentStatus.Pending),
                NewPayment(seed, new DateOnly(2026, 9, 30), 300m, PaymentStatus.Paid),
                NewPayment(seed, new DateOnly(2026, 10, 1), 400m, PaymentStatus.Pending));
            await db.SaveChangesAsync();
        });

        using var client = await LoginAsync(app, "admin.summary-month@test.local");

        // Uma data no meio do mês filtra o mês inteiro, não só aquele dia.
        var summary = await client.GetFromJsonAsync<List<JsonElement>>(
            "/api/payments/summary?referenceMonth=2026-09-15");

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Count);
        AssertSummary(summary[0], "Pending", 2, 300m);
        AssertSummary(summary[1], "Paid", 1, 300m);
        AssertSummary(summary[2], "Cancelled", 0, 0m);
    }

    [Fact]
    public async Task Summary_rejects_an_invalid_reference_month()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app, "admin.summary-invalid@test.local");

        using var client = await LoginAsync(app, "admin.summary-invalid@test.local");

        var response = await client.GetAsync("/api/payments/summary?referenceMonth=09-2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "O mês de referência deve usar o formato YYYY-MM-DD.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Summary_is_forbidden_for_professionals()
    {
        await using var app = new MultiClinicaFactory();
        await SeedAsync(app, "admin.summary-role@test.local", professionalEmail: "prof.summary@test.local");

        using var client = await LoginAsync(app, "prof.summary@test.local");

        var response = await client.GetAsync("/api/payments/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static void AssertSummary(JsonElement item, string status, int count, decimal total)
    {
        Assert.Equal(status, item.GetProperty("status").GetString());
        Assert.Equal(count, item.GetProperty("count").GetInt32());
        Assert.Equal(total, item.GetProperty("total").GetDecimal());
    }

    private sealed record SeedResult(
        int ClinicId, int AdminId, int PatientId, int PlanId, decimal PendingTotal, decimal PaidTotal);

    private static Payment NewPayment(
        SeedResult seed, DateOnly referenceMonth, decimal amount, PaymentStatus status) => new()
    {
        ClinicaId = seed.ClinicId,
        UserId = seed.AdminId,
        PatientId = seed.PatientId,
        PlanId = seed.PlanId,
        ReferenceMonth = referenceMonth,
        Amount = amount,
        PaymentMethod = "Pix",
        Status = status,
    };

    private static async Task<SeedResult> SeedAsync(
        MultiClinicaFactory app,
        string adminEmail,
        bool seedPayments = true,
        string? professionalEmail = null)
    {
        var result = new SeedResult(0, 0, 0, 0, 0m, 0m);

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Resumo", NomeResponsavel = "Victor" };
            var otherClinic = new Clinica { Nome = "Outra Clínica", NomeResponsavel = "Victor" };
            db.Clinicas.AddRange(clinic, otherClinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", adminEmail, UserRole.Administrador);
            db.Users.Add(admin);
            if (professionalEmail is not null)
                db.Users.Add(NewUser(clinic.Id, "Profissional", professionalEmail, UserRole.Profissional));

            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente", IsActive = true };
            var otherPatient = new Patient { ClinicaId = otherClinic.Id, Name = "Paciente de Fora", IsActive = true };
            db.Patients.AddRange(patient, otherPatient);
            await db.SaveChangesAsync();

            var sessionType = new SessionType { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            db.SessionTypes.Add(sessionType);
            await db.SaveChangesAsync();

            var plan = new Plans
            {
                ClinicaId = clinic.Id,
                Name = "Mensal",
                Valor = 150m,
                TipoPlano = TipoPlano.Mensal,
                TipoSessaoId = sessionType.Id,
            };
            var otherPlan = new Plans
            {
                ClinicaId = otherClinic.Id,
                Name = "Mensal",
                Valor = 999m,
                TipoPlano = TipoPlano.Mensal,
                TipoSessaoId = sessionType.Id,
            };
            db.Plans.AddRange(plan, otherPlan);
            await db.SaveChangesAsync();

            if (seedPayments)
            {
                db.Payments.AddRange(
                    new Payment
                    {
                        ClinicaId = clinic.Id,
                        UserId = admin.Id,
                        PatientId = patient.Id,
                        PlanId = plan.Id,
                        ReferenceMonth = new DateOnly(2026, 9, 1),
                        Amount = 100m,
                        PaymentMethod = "Pix",
                        Status = PaymentStatus.Pending,
                    },
                    new Payment
                    {
                        ClinicaId = clinic.Id,
                        UserId = admin.Id,
                        PatientId = patient.Id,
                        PlanId = plan.Id,
                        ReferenceMonth = new DateOnly(2026, 9, 2),
                        Amount = 150m,
                        PaymentMethod = "Pix",
                        Status = PaymentStatus.Pending,
                    },
                    new Payment
                    {
                        ClinicaId = clinic.Id,
                        UserId = admin.Id,
                        PatientId = patient.Id,
                        PlanId = plan.Id,
                        ReferenceMonth = new DateOnly(2026, 9, 3),
                        Amount = 200m,
                        PaymentMethod = "Pix",
                        Status = PaymentStatus.Pending,
                    },
                    new Payment
                    {
                        ClinicaId = clinic.Id,
                        UserId = admin.Id,
                        PatientId = patient.Id,
                        PlanId = plan.Id,
                        ReferenceMonth = new DateOnly(2026, 9, 4),
                        Amount = 300m,
                        PaymentMethod = "Pix",
                        Status = PaymentStatus.Paid,
                    },
                    new Payment
                    {
                        ClinicaId = clinic.Id,
                        UserId = admin.Id,
                        PatientId = patient.Id,
                        PlanId = plan.Id,
                        ReferenceMonth = new DateOnly(2026, 9, 5),
                        Amount = 400m,
                        PaymentMethod = "Pix",
                        Status = PaymentStatus.Paid,
                    },
                    new Payment
                    {
                        ClinicaId = otherClinic.Id,
                        UserId = admin.Id,
                        PatientId = otherPatient.Id,
                        PlanId = otherPlan.Id,
                        ReferenceMonth = new DateOnly(2026, 9, 6),
                        Amount = 900m,
                        PaymentMethod = "Pix",
                        Status = PaymentStatus.Pending,
                    });
                await db.SaveChangesAsync();
            }

            result = new SeedResult(clinic.Id, admin.Id, patient.Id, plan.Id, 450m, 700m);
        });

        return result;
    }

    private static User NewUser(int clinicId, string name, string email, UserRole role) => new()
    {
        ClinicaId = clinicId,
        Name = name,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        Role = role,
    };

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = Password,
        });
        response.EnsureSuccessStatusCode();
        return client;
    }
}
