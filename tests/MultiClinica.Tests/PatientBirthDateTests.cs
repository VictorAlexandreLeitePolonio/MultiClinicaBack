using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PatientBirthDateTests
{
    [Fact]
    public async Task CreateAndReadPatient_PreservesCivilBirthDateInDetailAndProfile()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicAsync(app, "admin.birth-date@test.local");

        using var client = await LoginAsync(app, "admin.birth-date@test.local");
        var create = await client.PostAsJsonAsync("/api/patients", NewPatient("2000-02-29"));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = created.GetProperty("patientId").GetInt32();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/patients/{patientId}");
        var profile = await client.GetFromJsonAsync<JsonElement>($"/api/patients/{patientId}/profile");

        Assert.Equal("2000-02-29", detail.GetProperty("birthDate").GetString());
        Assert.Equal("2000-02-29", profile.GetProperty("birthDate").GetString());
    }

    [Fact]
    public async Task UpdatePatient_OmittedBirthDatePreservesIt_AndNullClearsIt()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicAsync(app, "admin.birth-date-update@test.local");

        using var client = await LoginAsync(app, "admin.birth-date-update@test.local");
        var create = await client.PostAsJsonAsync("/api/patients", NewPatient("1998-07-21"));
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var patientId = created.GetProperty("patientId").GetInt32();

        var omitted = await client.PutAsJsonAsync($"/api/patients/{patientId}", NewPatientPayload());
        Assert.Equal(HttpStatusCode.OK, omitted.StatusCode);

        var preserved = await client.GetFromJsonAsync<JsonElement>($"/api/patients/{patientId}");
        Assert.Equal("1998-07-21", preserved.GetProperty("birthDate").GetString());

        var clearedPayload = NewPatientPayload();
        clearedPayload["birthDate"] = null;
        var cleared = await client.PutAsJsonAsync($"/api/patients/{patientId}", clearedPayload);
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);

        var afterClear = await client.GetFromJsonAsync<JsonElement>($"/api/patients/{patientId}");
        Assert.Equal(JsonValueKind.Null, afterClear.GetProperty("birthDate").ValueKind);
    }

    [Fact]
    public async Task CreatePatient_WithFutureBirthDate_ReturnsBadRequest()
    {
        await using var app = new MultiClinicaFactory();
        await SeedClinicAsync(app, "admin.birth-date-future@test.local");

        using var client = await LoginAsync(app, "admin.birth-date-future@test.local");
        var response = await client.PostAsJsonAsync("/api/patients", NewPatient("2999-01-01"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Dictionary<string, object?> NewPatient(string birthDate)
    {
        var payload = NewPatientPayload();
        payload["birthDate"] = birthDate;
        return payload;
    }

    private static Dictionary<string, object?> NewPatientPayload() => new()
    {
        ["name"] = "Paciente Teste",
        ["email"] = "patient.birth-date@test.local",
        ["cpf"] = "12345678909",
        ["phone"] = "11999999999",
        ["rg"] = null,
        ["rua"] = null,
        ["numero"] = null,
        ["bairro"] = null,
        ["cidade"] = null,
        ["estado"] = null,
        ["cep"] = null,
    };

    private static async Task SeedClinicAsync(MultiClinicaFactory app, string adminEmail)
    {
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Birth Date", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador,
            });
            await db.SaveChangesAsync();
        });
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
