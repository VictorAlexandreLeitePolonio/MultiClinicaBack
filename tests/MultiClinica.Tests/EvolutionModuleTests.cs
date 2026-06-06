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
    public async Task Draft_evolution_does_not_require_required_template_fields()
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
            db.Patients.Add(new Patient
            {
                ClinicaId = clinic.Id,
                Name = "Paciente A"
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var templateResponse = await client.PostAsJsonAsync("/api/evolution-templates", new
        {
            name = "Evolução Padrão"
        });
        templateResponse.EnsureSuccessStatusCode();
        var template = await templateResponse.Content.ReadFromJsonAsync<IdResponse>();

        var fieldResponse = await client.PostAsJsonAsync($"/api/evolution-templates/{template!.Id}/fields", new
        {
            label = "Dor",
            type = "Scale",
            required = true
        });
        fieldResponse.EnsureSuccessStatusCode();

        var treatmentResponse = await client.PostAsJsonAsync("/api/patients/1/treatments", new
        {
            templateId = template.Id,
            title = "Reabilitação joelho direito"
        });
        treatmentResponse.EnsureSuccessStatusCode();
        var treatment = await treatmentResponse.Content.ReadFromJsonAsync<IdResponse>();

        var evolutionResponse = await client.PostAsJsonAsync($"/api/patients/1/treatments/{treatment!.Id}/evolutions", new
        {
            status = "Draft",
            description = "Registro inicial em andamento",
            values = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Created, evolutionResponse.StatusCode);
        await app.SeedAsync(async db =>
        {
            var evolution = await db.PatientEvolutions.Include(e => e.Values).SingleAsync();
            Assert.Equal(EvolutionStatus.Draft, evolution.Status);
            Assert.Empty(evolution.Values);
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

    [Fact]
    public async Task Completed_evolution_requires_required_template_fields()
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
            db.Patients.Add(new Patient { ClinicaId = clinic.Id, Name = "Paciente A" });
            db.EvolutionTemplates.Add(new EvolutionTemplate
            {
                ClinicaId = clinic.Id,
                Name = "Evolução Padrão",
                Fields =
                [
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinic.Id,
                        Label = "Dor",
                        Key = "dor",
                        Type = EvolutionFieldType.Scale,
                        Unit = EvolutionFieldUnit.None,
                        MinValue = 0,
                        MaxValue = 10,
                        Required = true
                    }
                ]
            });
            await db.SaveChangesAsync();

            db.PatientTreatments.Add(new PatientTreatment
            {
                ClinicaId = clinic.Id,
                PatientId = 1,
                TemplateId = 1,
                ProfessionalId = 1,
                Title = "Reabilitação",
                StartedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/patients/1/treatments/1/evolutions", new
        {
            status = "Completed",
            values = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Campo obrigatório ausente", body);
    }

    [Fact]
    public async Task Clinic_user_cannot_create_treatment_with_template_from_another_clinic()
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
            db.Patients.Add(new Patient { ClinicaId = clinicA.Id, Name = "Paciente A" });
            db.EvolutionTemplates.Add(new EvolutionTemplate
            {
                ClinicaId = clinicB.Id,
                Name = "Template B"
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/patients/1/treatments", new
        {
            templateId = 1,
            title = "Tentativa cross tenant"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Evolution_rejects_field_outside_treatment_template()
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
            db.Patients.Add(new Patient { ClinicaId = clinic.Id, Name = "Paciente A" });
            db.EvolutionTemplates.AddRange(
                new EvolutionTemplate
                {
                    ClinicaId = clinic.Id,
                    Name = "Template A",
                    Fields =
                    [
                        new EvolutionTemplateField
                        {
                            ClinicaId = clinic.Id,
                            Label = "Dor",
                            Key = "dor",
                            Type = EvolutionFieldType.Scale,
                            MinValue = 0,
                            MaxValue = 10
                        }
                    ]
                },
                new EvolutionTemplate
                {
                    ClinicaId = clinic.Id,
                    Name = "Template B",
                    Fields =
                    [
                        new EvolutionTemplateField
                        {
                            ClinicaId = clinic.Id,
                            Label = "Força",
                            Key = "forca",
                            Type = EvolutionFieldType.Number
                        }
                    ]
                });
            await db.SaveChangesAsync();

            db.PatientTreatments.Add(new PatientTreatment
            {
                ClinicaId = clinic.Id,
                PatientId = 1,
                TemplateId = 1,
                ProfessionalId = 1,
                Title = "Reabilitação",
                StartedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/patients/1/treatments/1/evolutions", new
        {
            status = "Completed",
            values = new[]
            {
                new { fieldId = 2, valueNumber = 3 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Campo não pertence", body);
    }

    [Fact]
    public async Task Evolution_without_professional_id_uses_authenticated_user()
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
                Name = "Profissional A",
                Email = "pro-a@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Profissional
            });
            db.Patients.Add(new Patient { ClinicaId = clinic.Id, Name = "Paciente A" });
            db.EvolutionTemplates.Add(new EvolutionTemplate
            {
                ClinicaId = clinic.Id,
                Name = "Template A",
                Fields =
                [
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinic.Id,
                        Label = "Dor",
                        Key = "dor",
                        Type = EvolutionFieldType.Scale,
                        MinValue = 0,
                        MaxValue = 10,
                        Required = true
                    }
                ]
            });
            await db.SaveChangesAsync();

            db.PatientTreatments.Add(new PatientTreatment
            {
                ClinicaId = clinic.Id,
                PatientId = 1,
                TemplateId = 1,
                ProfessionalId = 1,
                Title = "Reabilitação",
                StartedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "pro-a@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/patients/1/treatments/1/evolutions", new
        {
            status = "Completed",
            values = new[]
            {
                new { fieldId = 1, valueNumber = 5 }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var evolution = await db.PatientEvolutions.SingleAsync();
            Assert.Equal(1, evolution.ProfessionalId);
        });
    }

    [Fact]
    public async Task Recepcao_cannot_create_patient_evolution()
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
                Name = "Recepção A",
                Email = "recepcao-a@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Recepcao
            });
            db.Patients.Add(new Patient { ClinicaId = clinic.Id, Name = "Paciente A" });
            db.EvolutionTemplates.Add(new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Template A" });
            await db.SaveChangesAsync();
            db.PatientTreatments.Add(new PatientTreatment
            {
                ClinicaId = clinic.Id,
                PatientId = 1,
                TemplateId = 1,
                Title = "Reabilitação",
                StartedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "recepcao-a@test.local", "secret123");

        var getResponse = await client.GetAsync("/api/patients/1/treatments");
        var postResponse = await client.PostAsJsonAsync("/api/patients/1/treatments/1/evolutions", new
        {
            status = "Draft"
        });

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task Treatment_progress_uses_only_completed_numeric_values_from_same_treatment()
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
                Name = "Profissional A",
                Email = "pro-a@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Profissional
            });
            db.Patients.Add(new Patient { ClinicaId = clinic.Id, Name = "Paciente A" });
            db.EvolutionTemplates.Add(new EvolutionTemplate
            {
                ClinicaId = clinic.Id,
                Name = "Fisioterapia",
                Fields =
                [
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinic.Id,
                        Label = "Dor",
                        Key = "dor",
                        Type = EvolutionFieldType.Scale,
                        Unit = EvolutionFieldUnit.Points,
                        MinValue = 0,
                        MaxValue = 10,
                        TargetValue = 0,
                        ExpectedDirection = EvolutionDirection.Decrease,
                        ShowInChart = true
                    },
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinic.Id,
                        Label = "Observação",
                        Key = "observacao",
                        Type = EvolutionFieldType.Text,
                        ShowInChart = false
                    }
                ]
            });
            await db.SaveChangesAsync();

            db.PatientTreatments.AddRange(
                new PatientTreatment
                {
                    ClinicaId = clinic.Id,
                    PatientId = 1,
                    TemplateId = 1,
                    ProfessionalId = 1,
                    Title = "Joelho direito",
                    StartedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc)
                },
                new PatientTreatment
                {
                    ClinicaId = clinic.Id,
                    PatientId = 1,
                    TemplateId = 1,
                    ProfessionalId = 1,
                    Title = "Ombro esquerdo",
                    StartedAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc)
                });
            await db.SaveChangesAsync();

            db.PatientEvolutions.AddRange(
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = 1,
                    TreatmentId = 1,
                    ProfessionalId = 1,
                    Date = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                    Status = EvolutionStatus.Completed,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = 1, ValueNumber = 8 },
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = 2, ValueText = "Primeira sessão" }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = 1,
                    TreatmentId = 1,
                    ProfessionalId = 1,
                    Date = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
                    Status = EvolutionStatus.Completed,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = 1, ValueNumber = 4 }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = 1,
                    TreatmentId = 1,
                    ProfessionalId = 1,
                    Date = new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc),
                    Status = EvolutionStatus.Draft,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = 1, ValueNumber = 1 }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = 1,
                    TreatmentId = 2,
                    ProfessionalId = 1,
                    Date = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
                    Status = EvolutionStatus.Completed,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = 1, ValueNumber = 2 }
                    ]
                });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "pro-a@test.local", "secret123");

        var response = await client.GetAsync("/api/patients/1/treatments/1/progress");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var progress = await response.Content.ReadFromJsonAsync<TreatmentProgressResponse>();

        Assert.NotNull(progress);
        Assert.Equal(1, progress.Treatment.Id);
        Assert.Equal("Joelho direito", progress.Treatment.Title);
        Assert.Equal(2, progress.Summary.TotalEvolutions);
        Assert.Equal(50, progress.Summary.OverallProgress);
        Assert.Equal(1, progress.Summary.ImprovingFields);
        Assert.Equal(0, progress.Summary.WorseningFields);
        Assert.Equal(new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc), progress.Summary.LastEvolutionDate);

        var chart = Assert.Single(progress.Charts);
        Assert.Equal("Dor", chart.Label);
        Assert.Equal("Points", chart.Unit);
        Assert.Equal("Decrease", chart.Direction);
        Assert.Equal(8, chart.InitialValue);
        Assert.Equal(4, chart.CurrentValue);
        Assert.Equal(0, chart.TargetValue);
        Assert.Equal(50, chart.Progress);
        Assert.Equal(2, chart.Data.Count);
        Assert.Equal(8, chart.Data[0].Value);
        Assert.Equal(4, chart.Data[1].Value);
    }

    [Fact]
    public async Task Treatment_progress_returns_404_for_treatment_from_another_clinic()
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
            db.Patients.AddRange(
                new Patient { ClinicaId = clinicA.Id, Name = "Paciente A" },
                new Patient { ClinicaId = clinicB.Id, Name = "Paciente B" });
            db.EvolutionTemplates.Add(new EvolutionTemplate { ClinicaId = clinicB.Id, Name = "Template B" });
            await db.SaveChangesAsync();

            db.PatientTreatments.Add(new PatientTreatment
            {
                ClinicaId = clinicB.Id,
                PatientId = 2,
                TemplateId = 1,
                Title = "Tratamento B",
                StartedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.GetAsync("/api/patients/2/treatments/1/progress");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Evolution_dashboard_summary_aggregates_only_authenticated_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;
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
            db.Patients.AddRange(
                new Patient { ClinicaId = clinicA.Id, Name = "Paciente A" },
                new Patient { ClinicaId = clinicB.Id, Name = "Paciente B" });
            db.EvolutionTemplates.AddRange(
                new EvolutionTemplate
                {
                    ClinicaId = clinicA.Id,
                    Name = "Fisioterapia",
                    Fields =
                    [
                        new EvolutionTemplateField
                        {
                            ClinicaId = clinicA.Id,
                            Label = "Dor",
                            Key = "dor",
                            Type = EvolutionFieldType.Scale,
                            Unit = EvolutionFieldUnit.Points,
                            MinValue = 0,
                            MaxValue = 10,
                            TargetValue = 0,
                            ExpectedDirection = EvolutionDirection.Decrease,
                            ShowInChart = true
                        }
                    ]
                },
                new EvolutionTemplate
                {
                    ClinicaId = clinicB.Id,
                    Name = "Template B"
                });
            await db.SaveChangesAsync();

            db.PatientTreatments.AddRange(
                new PatientTreatment
                {
                    ClinicaId = clinicA.Id,
                    PatientId = 1,
                    TemplateId = 1,
                    ProfessionalId = 1,
                    Title = "Joelho direito",
                    Status = TreatmentStatus.Active,
                    StartedAt = now.AddDays(-10)
                },
                new PatientTreatment
                {
                    ClinicaId = clinicA.Id,
                    PatientId = 1,
                    TemplateId = 1,
                    ProfessionalId = 1,
                    Title = "Ombro esquerdo",
                    Status = TreatmentStatus.Active,
                    StartedAt = now.AddDays(-5)
                },
                new PatientTreatment
                {
                    ClinicaId = clinicB.Id,
                    PatientId = 2,
                    TemplateId = 2,
                    Title = "Tratamento B",
                    Status = TreatmentStatus.Active,
                    StartedAt = now.AddDays(-5)
                });
            await db.SaveChangesAsync();

            db.PatientEvolutions.AddRange(
                new PatientEvolution
                {
                    ClinicaId = clinicA.Id,
                    PatientId = 1,
                    TreatmentId = 1,
                    ProfessionalId = 1,
                    Date = now.AddDays(-1),
                    Status = EvolutionStatus.Completed,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinicA.Id, FieldId = 1, ValueNumber = 8 }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinicA.Id,
                    PatientId = 1,
                    TreatmentId = 1,
                    ProfessionalId = 1,
                    Date = now,
                    Status = EvolutionStatus.Completed,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinicA.Id, FieldId = 1, ValueNumber = 4 }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinicA.Id,
                    PatientId = 1,
                    TreatmentId = 1,
                    ProfessionalId = 1,
                    Date = now,
                    Status = EvolutionStatus.Draft,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinicA.Id, FieldId = 1, ValueNumber = 1 }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinicB.Id,
                    PatientId = 2,
                    TreatmentId = 3,
                    ProfessionalId = 1,
                    Date = now,
                    Status = EvolutionStatus.Completed
                });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.GetAsync("/api/dashboard/evolution-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<EvolutionDashboardSummaryResponse>();

        Assert.NotNull(summary);
        Assert.Equal(2, summary.ActiveTreatments);
        Assert.Equal(2, summary.CompletedEvolutionsThisMonth);
        Assert.Equal(1, summary.PatientsImproving);
        Assert.Equal(0, summary.PatientsStable);
        Assert.Equal(0, summary.PatientsWorsening);
        Assert.Equal(50, summary.AverageProgress);

        var template = Assert.Single(summary.MostUsedTemplates);
        Assert.Equal(1, template.TemplateId);
        Assert.Equal("Fisioterapia", template.Name);
        Assert.Equal(2, template.Count);
    }

    [Fact]
    public async Task Recepcao_can_read_evolution_dashboard_summary()
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
                Name = "Recepção A",
                Email = "recepcao-a@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Recepcao
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "recepcao-a@test.local", "secret123");

        var response = await client.GetAsync("/api/dashboard/evolution-summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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

    private sealed class TreatmentProgressResponse
    {
        public ProgressTreatment Treatment { get; set; } = new();
        public ProgressSummary Summary { get; set; } = new();
        public List<ProgressChart> Charts { get; set; } = [];
    }

    private sealed class ProgressTreatment
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    private sealed class ProgressSummary
    {
        public int TotalEvolutions { get; set; }
        public decimal? OverallProgress { get; set; }
        public int ImprovingFields { get; set; }
        public int WorseningFields { get; set; }
        public int StableFields { get; set; }
        public DateTime? LastEvolutionDate { get; set; }
    }

    private sealed class ProgressChart
    {
        public int FieldId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal InitialValue { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal? TargetValue { get; set; }
        public decimal? Progress { get; set; }
        public List<ProgressPoint> Data { get; set; } = [];
    }

    private sealed class ProgressPoint
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
    }

    private sealed class EvolutionDashboardSummaryResponse
    {
        public int ActiveTreatments { get; set; }
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
