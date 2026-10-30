using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;
using Xunit;

namespace MultiClinica.Tests;

public class SignupTests
{
    private const string Password = "senha-forte";

    private static MultiClinicaFactory CreateApp(CapturingEmailSender email) => new()
    {
        ConfigureTestServices = services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(email);
        },
    };

    private static Task<HttpResponseMessage> SignupAsync(
        HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/public/signup", new
        {
            clinicName = "Clínica Nova",
            ownerName = "Ana",
            email,
            phone = "11999999999",
            password,
        });

    private static string Token(string body) => Regex.Match(body, @"token=([0-9A-F]+)").Groups[1].Value;

    [Fact]
    public async Task Public_plans_follow_the_plan_matrix()
    {
        await using var app = CreateApp(new CapturingEmailSender());
        using var client = app.CreateClient();

        var response = await client.GetAsync("/api/public/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var root = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var plans = root.GetProperty("plans");
        Assert.Equal(3, plans.GetArrayLength());
        Assert.Equal(["Free", "Pro", "Premium"],
            plans.EnumerateArray().Select(plan => plan.GetProperty("plan").GetString()));

        var pro = plans.EnumerateArray().Single(plan => plan.GetProperty("plan").GetString() == "Pro");
        Assert.Equal(49.90m, pro.GetProperty("monthlyPrice").GetDecimal());
        Assert.Equal(499.00m, pro.GetProperty("annualPrice").GetDecimal());

        var premium = plans.EnumerateArray().Single(plan => plan.GetProperty("plan").GetString() == "Premium");
        Assert.Equal(JsonValueKind.Null, premium.GetProperty("includedProfessionals").ValueKind);
        Assert.Equal(800, premium.GetProperty("quotas").GetProperty("WhatsAppConversations").GetInt32());
        Assert.Equal(1200, premium.GetProperty("quotas").GetProperty("TranscriptionMinutes").GetInt32());

        Assert.Equal(3, root.GetProperty("packs").GetArrayLength());
        Assert.Equal(59m, root.GetProperty("extraProfessionalPrice").GetDecimal());
    }

    [Fact]
    public async Task Signup_creates_free_clinic_with_inactive_admin_and_sends_email()
    {
        var email = new CapturingEmailSender();
        await using var app = CreateApp(email);
        using var client = app.CreateClient();

        var response = await SignupAsync(client, "nova@test.local");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(c => c.Email == "nova@test.local");
            Assert.Equal(ClinicPlan.Free, clinic.Plan);
            Assert.False(clinic.IsHubApproved);
            var admin = await db.Users.SingleAsync(u => u.Email == "nova@test.local");
            Assert.Equal(UserRole.Administrador, admin.Role);
            Assert.False(admin.IsActive);
            Assert.NotNull(admin.InvitationTokenHash);
            Assert.Contains(db.CommercialHistoryEvents,
                e => e.ClinicaId == clinic.Id && e.Type == CommercialHistoryEventType.ClinicCreated);
        });
        var message = Assert.Single(email.Messages);
        Assert.Equal("nova@test.local", message.To);
        Assert.Contains("/cadastro/confirmar?token=", message.Body);
    }

    [Fact]
    public async Task Login_fails_before_confirmation()
    {
        var email = new CapturingEmailSender();
        await using var app = CreateApp(email);
        using var client = app.CreateClient();
        (await SignupAsync(client, "pendente@test.local")).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = "pendente@test.local", Password = Password });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_activates_admin_and_token_cannot_be_reused()
    {
        var email = new CapturingEmailSender();
        await using var app = CreateApp(email);
        using var client = app.CreateClient();
        (await SignupAsync(client, "confirmar@test.local")).EnsureSuccessStatusCode();
        var token = Token(Assert.Single(email.Messages).Body);

        var confirm = await client.PostAsJsonAsync("/api/public/signup/confirm", new { token });

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var body = JsonDocument.Parse(await confirm.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("confirmar@test.local", body.GetProperty("email").GetString());
        (await client.PostAsJsonAsync("/api/auth/login",
            new LoginDto { Email = "confirmar@test.local", Password = Password })).EnsureSuccessStatusCode();

        var again = await client.PostAsJsonAsync("/api/public/signup/confirm", new { token });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        var error = JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("INVALID_TOKEN", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var email = new CapturingEmailSender();
        await using var app = CreateApp(email);
        using var client = app.CreateClient();
        (await SignupAsync(client, "expirado@test.local")).EnsureSuccessStatusCode();
        var token = Token(Assert.Single(email.Messages).Body);
        await app.SeedAsync(async db =>
        {
            var admin = await db.Users.SingleAsync(u => u.Email == "expirado@test.local");
            admin.InvitationExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });

        var response = await client.PostAsJsonAsync("/api/public/signup/confirm", new { token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("INVALID_TOKEN", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Existing_email_gets_same_response_without_new_clinic_or_email()
    {
        var email = new CapturingEmailSender();
        await using var app = CreateApp(email);
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Existente",
                NomeFantasia = "Existente",
                NomeResponsavel = "Ana",
                Email = "existente@test.local",
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Ana",
                Email = "existente@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Administrador,
            });
            await db.SaveChangesAsync();
        });
        using var client = app.CreateClient();

        var response = await SignupAsync(client, "existente@test.local");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await app.SeedAsync(async db =>
            Assert.Equal(1, await db.Clinicas.CountAsync(c => c.Email == "existente@test.local")));
        Assert.Empty(email.Messages);
    }

    [Fact]
    public async Task Short_password_is_rejected()
    {
        await using var app = CreateApp(new CapturingEmailSender());
        using var client = app.CreateClient();

        var response = await SignupAsync(client, "curta@test.local", password: "1234567");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resend_is_accepted_and_rotates_the_token()
    {
        var email = new CapturingEmailSender();
        await using var app = CreateApp(email);
        using var client = app.CreateClient();
        (await SignupAsync(client, "reenviar@test.local")).EnsureSuccessStatusCode();
        var firstToken = Token(Assert.Single(email.Messages).Body);

        var response = await client.PostAsJsonAsync("/api/public/signup/resend",
            new { email = "reenviar@test.local" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(2, email.Messages.Count);
        var secondToken = Token(email.Messages.Last().Body);
        Assert.NotEqual(firstToken, secondToken);
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Messages { get; } = [];

        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            Messages.Add((to, subject, htmlBody));
            return Task.CompletedTask;
        }
    }
}
