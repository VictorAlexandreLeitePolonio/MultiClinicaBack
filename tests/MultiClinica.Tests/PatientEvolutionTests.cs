using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PatientEvolutionTests
{
    [Fact]
    public async Task Draft_evolution_does_not_require_required_fields()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync($"/api/patients/{seed.PatientId}/evolutions", new
        {
            status = "Draft",
            description = "Registro inicial em andamento",
            values = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var evolution = await db.PatientEvolutions.Include(e => e.Values).SingleAsync();
            Assert.Equal(EvolutionStatus.Draft, evolution.Status);
            Assert.Empty(evolution.Values);
        });
    }

    [Fact]
    public async Task Completed_evolution_requires_required_fields_only_from_used_templates()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);
        var url = $"/api/patients/{seed.PatientId}/evolutions";

        // Usa só a ficha "Escala de dor"; a ficha "Força muscular" não é exigida.
        var onlyPainTemplate = await client.PostAsJsonAsync(url, new
        {
            status = "Completed",
            values = new[] { new { fieldId = seed.PainFieldId, valueNumber = 7 } }
        });
        Assert.Equal(HttpStatusCode.Created, onlyPainTemplate.StatusCode);

        // O campo opcional usa a ficha, então o obrigatório "Dor" passa a ser exigido.
        var missingRequired = await client.PostAsJsonAsync(url, new
        {
            status = "Completed",
            values = new[] { new { fieldId = seed.PainNoteFieldId, valueText = "Sem dor em repouso" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, missingRequired.StatusCode);
        var body = await missingRequired.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("EMPTY_FIELD", body!.Code);
        Assert.Equal("Preencha os campos obrigatórios da ficha Escala de dor.", body.Message);
    }

    [Fact]
    public async Task Evolution_rejects_field_from_another_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync($"/api/patients/{seed.PatientId}/evolutions", new
        {
            status = "Draft",
            values = new[] { new { fieldId = seed.OtherClinicFieldId, valueText = "Tentativa cross tenant" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("INVALID_VALUE", body!.Code);
    }

    [Fact]
    public async Task Evolution_rejects_inactive_field()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync($"/api/patients/{seed.PatientId}/evolutions", new
        {
            status = "Draft",
            values = new[] { new { fieldId = seed.InactiveFieldId, valueText = "Campo desativado" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("INVALID_VALUE", body!.Code);
    }

    [Fact]
    public async Task Evolution_without_professional_id_uses_authenticated_user()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync($"/api/patients/{seed.PatientId}/evolutions", new
        {
            status = "Draft",
            values = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EvolutionResponse>();
        Assert.Equal(seed.ProfessionalId, created!.ProfessionalId);
        await app.SeedAsync(async db =>
        {
            var evolution = await db.PatientEvolutions.SingleAsync();
            Assert.Equal(seed.ProfessionalId, evolution.ProfessionalId);
        });
    }

    [Fact]
    public async Task Recepcao_cannot_read_or_create_patient_evolutions()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ReceptionEmail);
        var url = $"/api/patients/{seed.PatientId}/evolutions";

        var getResponse = await client.GetAsync(url);
        var postResponse = await client.PostAsJsonAsync(url, new { status = "Draft" });

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task Evolution_with_values_from_two_templates_reports_both_template_names()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);
        var url = $"/api/patients/{seed.PatientId}/evolutions";

        var response = await client.PostAsJsonAsync(url, new
        {
            status = "Completed",
            values = new[]
            {
                new { fieldId = seed.PainFieldId, valueNumber = 6 },
                new { fieldId = seed.StrengthFieldId, valueNumber = 32 }
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EvolutionResponse>();
        Assert.Equal(["Escala de dor", "Força muscular"], created!.TemplateNames);
        Assert.Equal(2, created.Values.Count);

        var detail = await client.GetFromJsonAsync<EvolutionResponse>(
            $"/api/patients/{seed.PatientId}/evolutions/{created.Id}");
        Assert.Equal(["Escala de dor", "Força muscular"], detail!.TemplateNames);
    }

    [Fact]
    public async Task Evolution_rejects_appointment_from_another_patient()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync($"/api/patients/{seed.PatientId}/evolutions", new
        {
            status = "Draft",
            appointmentId = seed.OtherPatientAppointmentId
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("INVALID_VALUE", body!.Code);
        Assert.Equal("A consulta não pertence a este paciente.", body.Message);
    }

    [Fact]
    public async Task Two_evolutions_on_the_same_appointment_are_returned_by_filter()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);
        var url = $"/api/patients/{seed.PatientId}/evolutions";

        var first = await client.PostAsJsonAsync(url, new
        {
            status = "Draft",
            appointmentId = seed.AppointmentId,
            description = "Primeiro aspecto"
        });
        var second = await client.PostAsJsonAsync(url, new
        {
            status = "Draft",
            appointmentId = seed.AppointmentId,
            description = "Segundo aspecto"
        });
        var withoutAppointment = await client.PostAsJsonAsync(url, new
        {
            status = "Draft",
            description = "Evolução avulsa"
        });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(HttpStatusCode.Created, withoutAppointment.StatusCode);

        var filtered = await client.GetFromJsonAsync<EvolutionListResponse>(
            $"{url}?appointmentId={seed.AppointmentId}");

        Assert.Equal(2, filtered!.TotalCount);
        Assert.Equal(2, filtered.Data.Count);
        Assert.All(filtered.Data, e => Assert.Equal(seed.AppointmentId, e.AppointmentId));
    }

    [Fact]
    public async Task Patch_replaces_evolution_values_with_soft_delete()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);
        var url = $"/api/patients/{seed.PatientId}/evolutions";

        var createResponse = await client.PostAsJsonAsync(url, new
        {
            status = "Completed",
            values = new[] { new { fieldId = seed.PainFieldId, valueNumber = 8 } }
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<EvolutionResponse>();

        var patchResponse = await client.PatchAsJsonAsync($"{url}/{created!.Id}", new
        {
            values = new[] { new { fieldId = seed.StrengthFieldId, valueNumber = 42 } }
        });

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var detail = await client.GetFromJsonAsync<EvolutionResponse>($"{url}/{created.Id}");
        Assert.Equal(["Força muscular"], detail!.TemplateNames);
        var value = Assert.Single(detail.Values);
        Assert.Equal(seed.StrengthFieldId, value.FieldId);
        Assert.Equal(42, value.ValueNumber);

        await app.SeedAsync(async db =>
        {
            var values = await db.PatientEvolutionValues
                .Where(v => v.EvolutionId == created.Id)
                .ToListAsync();
            Assert.Equal(2, values.Count);
            var oldValue = Assert.Single(values, v => v.FieldId == seed.PainFieldId);
            Assert.True(oldValue.IsDeleted);
            Assert.NotNull(oldValue.DeletedAt);
            Assert.False(Assert.Single(values, v => v.FieldId == seed.StrengthFieldId).IsDeleted);
        });
    }

    [Fact]
    public async Task Delete_removes_evolution_from_list_and_detail()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);
        var url = $"/api/patients/{seed.PatientId}/evolutions";

        var createResponse = await client.PostAsJsonAsync(url, new
        {
            status = "Draft",
            values = Array.Empty<object>()
        });
        var created = await createResponse.Content.ReadFromJsonAsync<EvolutionResponse>();

        var deleteResponse = await client.DeleteAsync($"{url}/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var list = await client.GetFromJsonAsync<EvolutionListResponse>(url);
        Assert.Equal(0, list!.TotalCount);
        Assert.Empty(list.Data);

        var detail = await client.GetAsync($"{url}/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);

        await app.SeedAsync(async db =>
        {
            var evolution = await db.PatientEvolutions.SingleAsync();
            Assert.True(evolution.IsDeleted);
            Assert.NotNull(evolution.DeletedAt);
        });
    }

    [Fact]
    public async Task List_orders_by_date_desc_and_paginates()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);
        var url = $"/api/patients/{seed.PatientId}/evolutions";

        var older = await client.PostAsJsonAsync(url, new
        {
            status = "Draft",
            date = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            description = "Mais antiga"
        });
        var newer = await client.PostAsJsonAsync(url, new
        {
            status = "Draft",
            date = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
            description = "Mais recente"
        });
        Assert.Equal(HttpStatusCode.Created, older.StatusCode);
        Assert.Equal(HttpStatusCode.Created, newer.StatusCode);

        var firstPage = await client.GetFromJsonAsync<EvolutionListResponse>($"{url}?page=1&pageSize=1");
        Assert.Equal(2, firstPage!.TotalCount);
        Assert.Equal("Mais recente", Assert.Single(firstPage.Data).Description);

        var secondPage = await client.GetFromJsonAsync<EvolutionListResponse>($"{url}?page=2&pageSize=1");
        Assert.Equal("Mais antiga", Assert.Single(secondPage!.Data).Description);
    }

    [Fact]
    public async Task List_returns_not_found_for_patient_from_another_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var seed = await SeedAsync(app);
        using var client = await LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.GetAsync($"/api/patients/{seed.OtherClinicPatientId}/evolutions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = "secret123"
        });

        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<SeedData> SeedAsync(MultiClinicaFactory app)
    {
        var seed = new SeedData();

        await app.SeedAsync(async db =>
        {
            var clinicA = new Clinica { Nome = "Clínica A", NomeResponsavel = "Admin A" };
            var clinicB = new Clinica { Nome = "Clínica B", NomeResponsavel = "Admin B" };
            db.Clinicas.AddRange(clinicA, clinicB);
            await db.SaveChangesAsync();

            var admin = new User
            {
                ClinicaId = clinicA.Id,
                Name = "Admin A",
                Email = seed.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            };
            var professional = new User
            {
                ClinicaId = clinicA.Id,
                Name = "Profissional A",
                Email = seed.ProfessionalEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Profissional
            };
            var reception = new User
            {
                ClinicaId = clinicA.Id,
                Name = "Recepção A",
                Email = seed.ReceptionEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Recepcao
            };
            db.Users.AddRange(admin, professional, reception);

            var patient = new Patient { ClinicaId = clinicA.Id, Name = "Paciente A" };
            var otherPatient = new Patient { ClinicaId = clinicA.Id, Name = "Paciente B" };
            var otherClinicPatient = new Patient { ClinicaId = clinicB.Id, Name = "Paciente C" };
            db.Patients.AddRange(patient, otherPatient, otherClinicPatient);

            var painTemplate = new EvolutionTemplate
            {
                ClinicaId = clinicA.Id,
                Name = "Escala de dor",
                Fields =
                [
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinicA.Id,
                        Label = "Dor",
                        Key = "dor",
                        Type = EvolutionFieldType.Scale,
                        MinValue = 0,
                        MaxValue = 10,
                        Required = true,
                        Order = 1
                    },
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinicA.Id,
                        Label = "Observação da dor",
                        Key = "observacao_dor",
                        Type = EvolutionFieldType.Text,
                        Order = 2
                    },
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinicA.Id,
                        Label = "Campo inativo",
                        Key = "campo_inativo",
                        Type = EvolutionFieldType.Text,
                        IsActive = false,
                        Order = 3
                    }
                ]
            };
            var strengthTemplate = new EvolutionTemplate
            {
                ClinicaId = clinicA.Id,
                Name = "Força muscular",
                Fields =
                [
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinicA.Id,
                        Label = "Força",
                        Key = "forca",
                        Type = EvolutionFieldType.Number,
                        MinValue = 0,
                        MaxValue = 100,
                        Required = true,
                        Order = 1
                    }
                ]
            };
            var otherClinicTemplate = new EvolutionTemplate
            {
                ClinicaId = clinicB.Id,
                Name = "Ficha de outra clínica",
                Fields =
                [
                    new EvolutionTemplateField
                    {
                        ClinicaId = clinicB.Id,
                        Label = "Campo B",
                        Key = "campo_b",
                        Type = EvolutionFieldType.Text,
                        Order = 1
                    }
                ]
            };
            db.EvolutionTemplates.AddRange(painTemplate, strengthTemplate, otherClinicTemplate);
            await db.SaveChangesAsync();

            var appointment = new Appointment
            {
                ClinicaId = clinicA.Id,
                UserId = professional.Id,
                PatientId = patient.Id,
                AppointmentDate = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                Status = AppointmentStatus.Scheduled
            };
            var otherPatientAppointment = new Appointment
            {
                ClinicaId = clinicA.Id,
                UserId = professional.Id,
                PatientId = otherPatient.Id,
                AppointmentDate = new DateTime(2026, 6, 2, 10, 0, 0, DateTimeKind.Utc),
                Status = AppointmentStatus.Scheduled
            };
            db.Appointments.AddRange(appointment, otherPatientAppointment);
            await db.SaveChangesAsync();

            seed.ClinicAId = clinicA.Id;
            seed.ClinicBId = clinicB.Id;
            seed.AdminId = admin.Id;
            seed.ProfessionalId = professional.Id;
            seed.ReceptionId = reception.Id;
            seed.PatientId = patient.Id;
            seed.OtherPatientId = otherPatient.Id;
            seed.OtherClinicPatientId = otherClinicPatient.Id;
            seed.PainTemplateId = painTemplate.Id;
            seed.StrengthTemplateId = strengthTemplate.Id;
            seed.PainFieldId = painTemplate.Fields.Single(f => f.Label == "Dor").Id;
            seed.PainNoteFieldId = painTemplate.Fields.Single(f => f.Label == "Observação da dor").Id;
            seed.InactiveFieldId = painTemplate.Fields.Single(f => f.Label == "Campo inativo").Id;
            seed.StrengthFieldId = strengthTemplate.Fields.Single(f => f.Label == "Força").Id;
            seed.OtherClinicFieldId = otherClinicTemplate.Fields.Single().Id;
            seed.AppointmentId = appointment.Id;
            seed.OtherPatientAppointmentId = otherPatientAppointment.Id;
        });

        return seed;
    }

    private sealed class SeedData
    {
        public string AdminEmail { get; } = "admin-a@test.local";
        public string ProfessionalEmail { get; } = "pro-a@test.local";
        public string ReceptionEmail { get; } = "recepcao-a@test.local";

        public int ClinicAId { get; set; }
        public int ClinicBId { get; set; }
        public int AdminId { get; set; }
        public int ProfessionalId { get; set; }
        public int ReceptionId { get; set; }
        public int PatientId { get; set; }
        public int OtherPatientId { get; set; }
        public int OtherClinicPatientId { get; set; }
        public int PainTemplateId { get; set; }
        public int StrengthTemplateId { get; set; }
        public int PainFieldId { get; set; }
        public int PainNoteFieldId { get; set; }
        public int InactiveFieldId { get; set; }
        public int StrengthFieldId { get; set; }
        public int OtherClinicFieldId { get; set; }
        public int AppointmentId { get; set; }
        public int OtherPatientAppointmentId { get; set; }
    }

    private sealed class EvolutionResponse
    {
        public int Id { get; set; }
        public int? AppointmentId { get; set; }
        public int ProfessionalId { get; set; }
        public string? Description { get; set; }
        public List<string> TemplateNames { get; set; } = [];
        public List<EvolutionValueResponse> Values { get; set; } = [];
    }

    private sealed class EvolutionValueResponse
    {
        public int FieldId { get; set; }
        public decimal? ValueNumber { get; set; }
        public string? ValueText { get; set; }
        public bool? ValueBoolean { get; set; }
    }

    private sealed class EvolutionListResponse
    {
        public List<EvolutionResponse> Data { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    private sealed class ErrorResponse
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
