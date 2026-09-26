using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.DTOs.User;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;
using Xunit;

namespace MultiClinica.Tests;

public class UserInvitationTests
{
    [Fact]
    public async Task Invite_EnforcesTenantAndRole_ThenActivatesOnlyOnce()
    {
        var email = new CapturingEmailSender();
        await using var app = new PatientImportFactory(emailSender: email);
        await SeedAsync(app);
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/user-invitations",
            new InviteUserDto { Name = "Ana", Email = "ana@test.local" })).StatusCode);
        using var admin = app.CreateClient();
        (await admin.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "admin@test.local", Password = "secret123" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/user-invitations",
            new InviteUserDto { Name = "Ana", Email = "ana@test.local", Role = UserRole.Administrador })).StatusCode);
        var response = await admin.PostAsJsonAsync("/api/user-invitations",
            new InviteUserDto { Name = "Ana <script>", Email = " ANA@test.local ", Role = UserRole.Profissional });
        response.EnsureSuccessStatusCode();
        var invitation = (await response.Content.ReadFromJsonAsync<UserInvitationResponseDto>())!;
        Assert.True(invitation.EmailSent);
        Assert.Contains("&lt;script&gt;", email.Body);
        var firstToken = Token(email.Body);
        using (var scope = app.Services.CreateScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == invitation.UserId);
            Assert.False(user.IsActive);
            Assert.NotEqual(firstToken, user.InvitationTokenHash);
            Assert.Equal("ana@test.local", user.Email);
        }
        using var otherAdmin = app.CreateClient();
        (await otherAdmin.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "other@test.local", Password = "secret123" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.PostAsync($"/api/user-invitations/{invitation.UserId}/resend", null)).StatusCode);
        (await admin.PostAsync($"/api/user-invitations/{invitation.UserId}/resend", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/user-invitations/accept",
            new AcceptUserInvitationDto { Token = firstToken, Password = "new-password" })).StatusCode);
        var payload = new AcceptUserInvitationDto { Token = Token(email.Body), Password = "new-password" };
        (await anonymous.PostAsJsonAsync("/api/user-invitations/accept", payload)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/user-invitations/accept", payload)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/user-invitations/{invitation.UserId}/resend", null)).StatusCode);
        (await anonymous.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "ana@test.local", Password = payload.Password })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task FailedEmail_IsRecoverable_AndExpiredInvitationCannotActivate()
    {
        var email = new CapturingEmailSender { Fail = true };
        await using var app = new PatientImportFactory(emailSender: email);
        await SeedAsync(app);
        using var admin = app.CreateClient();
        (await admin.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = "admin@test.local", Password = "secret123" })).EnsureSuccessStatusCode();
        var response = await admin.PostAsJsonAsync("/api/user-invitations",
            new InviteUserDto { Name = "Ana", Email = "ana@test.local" });
        response.EnsureSuccessStatusCode();
        var invitation = (await response.Content.ReadFromJsonAsync<UserInvitationResponseDto>())!;
        Assert.False(invitation.EmailSent);
        email.Fail = false;
        var resent = await admin.PostAsync($"/api/user-invitations/{invitation.UserId}/resend", null);
        Assert.True((await resent.Content.ReadFromJsonAsync<UserInvitationResponseDto>())!.EmailSent);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == invitation.UserId);
            user.InvitationExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using var anonymous = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/user-invitations/accept",
            new AcceptUserInvitationDto { Token = Token(email.Body), Password = "new-password" })).StatusCode);
    }

    private static string Token(string body) => Regex.Match(body, @"token=([A-F0-9]+)").Groups[1].Value;

    private static async Task SeedAsync(WebApplicationFactory<Program> app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        foreach (var address in new[] { "admin@test.local", "other@test.local" })
        {
            var clinic = new Clinica { Nome = "Clínica", NomeFantasia = "Clínica", NomeResponsavel = "Admin" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            db.Users.Add(new User { ClinicaId = clinic.Id, Name = "Admin", Email = address,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"), Role = UserRole.Administrador });
            await db.SaveChangesAsync();
        }
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public bool Fail { get; set; }
        public string Body { get; private set; } = "";
        public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new InvalidOperationException("Transport unavailable");
            Body = htmlBody;
            return Task.CompletedTask;
        }
    }
}
