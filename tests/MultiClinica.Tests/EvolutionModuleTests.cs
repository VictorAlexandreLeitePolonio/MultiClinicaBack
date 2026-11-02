using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class EvolutionModuleTests
{
    [Fact]
    public async Task Administrator_creates_evolution_template_using_authenticated_clinic()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinicA = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            var clinicB = new Clinica { Nome = "Clinica B", NomeResponsavel = "Victor" };
            db.Clinicas.AddRange(clinicA, clinicB);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinicA.Id,
                Name = "Admin A",
                Email = "admin-a@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/evolution-templates", new
        {
            clinicaId = 2,
            name = "Evolução Fisioterapia",
            description = "Modelo para fisioterapia",
            category = "Fisioterapia",
            isDefault = true
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var template = await db.EvolutionTemplates.SingleAsync();
            Assert.Equal(1, template.ClinicaId);
            Assert.Equal("Evolução Fisioterapia", template.Name);
            Assert.True(template.IsActive);
            Assert.False(template.IsDeleted);
        });
    }

    [Fact]
    public async Task Template_field_key_is_generated_from_label()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin A",
                Email = "admin-a@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            });
            db.EvolutionTemplates.Add(new EvolutionTemplate
            {
                ClinicaId = clinic.Id,
                Name = "Evolução Padrão"
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/evolution-templates/1/fields", new
        {
            label = "Dor no Ombro",
            type = "Scale"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var field = await db.EvolutionTemplateFields.SingleAsync();
            Assert.Equal("dor_no_ombro", field.Key);
            Assert.Equal(0, field.MinValue);
            Assert.Equal(10, field.MaxValue);
        });
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = password
        });

        response.EnsureSuccessStatusCode();
    }

    private sealed class IdResponse
    {
        public int Id { get; set; }
    }
}
