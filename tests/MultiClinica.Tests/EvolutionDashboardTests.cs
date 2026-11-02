using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class EvolutionDashboardTests
{
    private const string Password = "secret123";

    [Fact]
    public async Task Dashboard_aggregates_only_the_authenticated_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;

        await app.SeedAsync(async db =>
        {
            var clinicA = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            var clinicB = new Clinica { Nome = "Clínica B", NomeResponsavel = "Victor" };
            db.Clinicas.AddRange(clinicA, clinicB);
            await db.SaveChangesAsync();

            var adminA = NewUser(clinicA.Id, "Admin A", "admin-a@test.local", UserRole.Administrador);
            var adminB = NewUser(clinicB.Id, "Admin B", "admin-b@test.local", UserRole.Administrador);
            db.Users.AddRange(adminA, adminB);

            var patientA = new Patient { ClinicaId = clinicA.Id, Name = "Paciente A" };
            var patientB = new Patient { ClinicaId = clinicB.Id, Name = "Paciente B" };
            db.Patients.AddRange(patientA, patientB);
            await db.SaveChangesAsync();

            var templateA = new EvolutionTemplate { ClinicaId = clinicA.Id, Name = "Fisioterapia" };
            var templateB = new EvolutionTemplate { ClinicaId = clinicB.Id, Name = "Template B" };
            db.EvolutionTemplates.AddRange(templateA, templateB);
            await db.SaveChangesAsync();

            var fieldA = NewNumericField(clinicA.Id, templateA.Id, "Dor", EvolutionDirection.Decrease);
            var fieldB = NewNumericField(clinicB.Id, templateB.Id, "Dor B", EvolutionDirection.Decrease);
            db.EvolutionTemplateFields.AddRange(fieldA, fieldB);
            await db.SaveChangesAsync();

            db.PatientEvolutions.AddRange(
                CompletedEvolution(clinicA.Id, patientA.Id, adminA.Id, now.AddHours(-3),
                    (fieldA.Id, 8m)),
                CompletedEvolution(clinicA.Id, patientA.Id, adminA.Id, now,
                    (fieldA.Id, 4m)),
                new PatientEvolution
                {
                    ClinicaId = clinicA.Id,
                    PatientId = patientA.Id,
                    ProfessionalId = adminA.Id,
                    Date = now,
                    Status = EvolutionStatus.Draft,
                    Values = [new PatientEvolutionValue { ClinicaId = clinicA.Id, FieldId = fieldA.Id, ValueNumber = 1 }]
                },
                CompletedEvolution(clinicB.Id, patientB.Id, adminB.Id, now,
                    (fieldB.Id, 2m)));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local");

        var response = await client.GetAsync("/api/dashboard/evolution-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.PatientsInFollowUp);
        Assert.Equal(1, summary.PatientsImproving);
        Assert.Equal(0, summary.PatientsStable);
        Assert.Equal(0, summary.PatientsWorsening);
        Assert.Equal(50, summary.AverageProgress);

        var template = Assert.Single(summary.MostUsedTemplates);
        Assert.Equal("Fisioterapia", template.Name);
        Assert.Equal(2, template.Count);
    }

    [Fact]
    public async Task Reception_can_read_the_dashboard_summary()
    {
        await using var app = new MultiClinicaFactory();

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(NewUser(clinic.Id, "Recepção", "recepcao@test.local", UserRole.Recepcao));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "recepcao@test.local");

        var response = await client.GetAsync("/api/dashboard/evolution-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Patients_in_follow_up_and_classification_ignore_measurements_older_than_60_days()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin@test.local", UserRole.Administrador);
            db.Users.Add(admin);

            var recentPatient = new Patient { ClinicaId = clinic.Id, Name = "Recente" };
            var oldPatient = new Patient { ClinicaId = clinic.Id, Name = "Antigo" };
            var draftPatient = new Patient { ClinicaId = clinic.Id, Name = "Rascunho" };
            var noMeasurementPatient = new Patient { ClinicaId = clinic.Id, Name = "Sem medição" };
            db.Patients.AddRange(recentPatient, oldPatient, draftPatient, noMeasurementPatient);
            await db.SaveChangesAsync();

            var template = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            db.EvolutionTemplates.Add(template);
            await db.SaveChangesAsync();

            var field = NewNumericField(clinic.Id, template.Id, "Dor", EvolutionDirection.Decrease);
            db.EvolutionTemplateFields.Add(field);
            await db.SaveChangesAsync();

            db.PatientEvolutions.AddRange(
                CompletedEvolution(clinic.Id, recentPatient.Id, admin.Id, now, (field.Id, 5m)),
                CompletedEvolution(clinic.Id, oldPatient.Id, admin.Id, now.AddDays(-61), (field.Id, 5m)),
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = draftPatient.Id,
                    ProfessionalId = admin.Id,
                    Date = now,
                    Status = EvolutionStatus.Draft,
                    Values = [new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = field.Id, ValueNumber = 5 }]
                },
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = noMeasurementPatient.Id,
                    ProfessionalId = admin.Id,
                    Date = now,
                    Status = EvolutionStatus.Completed
                });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin@test.local");

        var response = await client.GetAsync("/api/dashboard/evolution-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.PatientsInFollowUp);
        Assert.Equal(1, summary.PatientsStable);
    }

    [Fact]
    public async Task Classification_improves_or_worsens_per_series()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin@test.local", UserRole.Administrador);
            db.Users.Add(admin);

            var improvingPatient = new Patient { ClinicaId = clinic.Id, Name = "Melhorando" };
            var worseningPatient = new Patient { ClinicaId = clinic.Id, Name = "Piorando" };
            db.Patients.AddRange(improvingPatient, worseningPatient);
            await db.SaveChangesAsync();

            var template = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            db.EvolutionTemplates.Add(template);
            await db.SaveChangesAsync();

            var field = NewNumericField(clinic.Id, template.Id, "Dor", EvolutionDirection.Decrease);
            db.EvolutionTemplateFields.Add(field);
            await db.SaveChangesAsync();

            db.PatientEvolutions.AddRange(
                CompletedEvolution(clinic.Id, improvingPatient.Id, admin.Id, now.AddHours(-4), (field.Id, 8m)),
                CompletedEvolution(clinic.Id, improvingPatient.Id, admin.Id, now.AddHours(-2), (field.Id, 4m)),
                CompletedEvolution(clinic.Id, worseningPatient.Id, admin.Id, now.AddHours(-3), (field.Id, 4m)),
                CompletedEvolution(clinic.Id, worseningPatient.Id, admin.Id, now.AddHours(-1), (field.Id, 8m)));
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin@test.local");

        var response = await client.GetAsync("/api/dashboard/evolution-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(2, summary.PatientsInFollowUp);
        Assert.Equal(1, summary.PatientsImproving);
        Assert.Equal(0, summary.PatientsStable);
        Assert.Equal(1, summary.PatientsWorsening);
        Assert.Equal(25, summary.AverageProgress);
    }

    [Fact]
    public async Task Most_used_templates_count_distinct_completed_evolutions()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin@test.local", UserRole.Administrador);
            db.Users.Add(admin);

            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente A" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var physiotherapy = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            var assessment = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Avaliação" };
            db.EvolutionTemplates.AddRange(physiotherapy, assessment);
            await db.SaveChangesAsync();

            var pain = NewNumericField(clinic.Id, physiotherapy.Id, "Dor", EvolutionDirection.Decrease);
            var mobility = NewNumericField(clinic.Id, physiotherapy.Id, "Mobilidade", EvolutionDirection.Increase);
            var strength = NewNumericField(clinic.Id, assessment.Id, "Força", EvolutionDirection.Increase);
            db.EvolutionTemplateFields.AddRange(pain, mobility, strength);
            await db.SaveChangesAsync();

            db.PatientEvolutions.AddRange(
                // Uma evolução com dois campos da mesma ficha conta uma vez para ela.
                CompletedEvolution(clinic.Id, patient.Id, admin.Id, now.AddHours(-3),
                    (pain.Id, 8m), (mobility.Id, 40m)),
                CompletedEvolution(clinic.Id, patient.Id, admin.Id, now.AddHours(-2),
                    (pain.Id, 4m), (strength.Id, 5m)),
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = patient.Id,
                    ProfessionalId = admin.Id,
                    Date = now.AddHours(-1),
                    Status = EvolutionStatus.Draft,
                    Values = [new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = pain.Id, ValueNumber = 1 }]
                });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin@test.local");

        var response = await client.GetAsync("/api/dashboard/evolution-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<DashboardSummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(2, summary.MostUsedTemplates.Count);
        Assert.Equal("Fisioterapia", summary.MostUsedTemplates[0].Name);
        Assert.Equal(2, summary.MostUsedTemplates[0].Count);
        Assert.Equal("Avaliação", summary.MostUsedTemplates[1].Name);
        Assert.Equal(1, summary.MostUsedTemplates[1].Count);
    }

    private static User NewUser(int clinicId, string name, string email, UserRole role) => new()
    {
        ClinicaId = clinicId,
        Name = name,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        Role = role
    };

    private static EvolutionTemplateField NewNumericField(
        int clinicId, int templateId, string label, EvolutionDirection direction) => new()
    {
        ClinicaId = clinicId,
        TemplateId = templateId,
        Label = label,
        Key = label.ToLowerInvariant(),
        Type = EvolutionFieldType.Scale,
        Unit = EvolutionFieldUnit.Points,
        MinValue = 0,
        MaxValue = 100,
        TargetValue = direction == EvolutionDirection.Decrease ? 0 : 100,
        ExpectedDirection = direction,
        ShowInChart = true
    };

    private static PatientEvolution CompletedEvolution(
        int clinicId, int patientId, int professionalId, DateTime date,
        params (int FieldId, decimal Value)[] values) => new()
    {
        ClinicaId = clinicId,
        PatientId = patientId,
        ProfessionalId = professionalId,
        Date = date,
        Status = EvolutionStatus.Completed,
        Values = values
            .Select(value => new PatientEvolutionValue
            {
                ClinicaId = clinicId,
                FieldId = value.FieldId,
                ValueNumber = value.Value
            })
            .ToList()
    };

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = Password
        });

        response.EnsureSuccessStatusCode();
    }

    private sealed class DashboardSummaryResponse
    {
        public int PatientsInFollowUp { get; set; }
        public int CompletedEvolutionsThisMonth { get; set; }
        public int PatientsImproving { get; set; }
        public int PatientsStable { get; set; }
        public int PatientsWorsening { get; set; }
        public decimal? AverageProgress { get; set; }
        public List<MostUsedTemplateResponse> MostUsedTemplates { get; set; } = [];
    }

    private sealed class MostUsedTemplateResponse
    {
        public int TemplateId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
