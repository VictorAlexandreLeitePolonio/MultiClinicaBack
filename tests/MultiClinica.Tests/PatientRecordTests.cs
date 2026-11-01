using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PatientRecordTests
{
    private const string Password = "secret123";

    [Fact]
    public async Task Measurements_group_by_template_ignore_drafts_and_keep_points_in_date_order()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;
        var ids = new RecordIds();

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

            var painTemplate = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Escala de dor" };
            var mobilityTemplate = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Amplitude" };
            db.EvolutionTemplates.AddRange(painTemplate, mobilityTemplate);
            await db.SaveChangesAsync();

            var pain = new EvolutionTemplateField
            {
                ClinicaId = clinic.Id,
                TemplateId = painTemplate.Id,
                Label = "Dor",
                Key = "dor",
                Type = EvolutionFieldType.Scale,
                Unit = EvolutionFieldUnit.Points,
                MinValue = 0,
                MaxValue = 10,
                TargetValue = 0,
                ExpectedDirection = EvolutionDirection.Decrease,
                ShowInChart = true
            };
            var flexion = new EvolutionTemplateField
            {
                ClinicaId = clinic.Id,
                TemplateId = mobilityTemplate.Id,
                Label = "Flexão",
                Key = "flexao",
                Type = EvolutionFieldType.Number,
                Unit = EvolutionFieldUnit.Degrees,
                ExpectedDirection = EvolutionDirection.Increase,
                ShowInChart = true
            };
            var notes = new EvolutionTemplateField
            {
                ClinicaId = clinic.Id,
                TemplateId = mobilityTemplate.Id,
                Label = "Observação",
                Key = "observacao",
                Type = EvolutionFieldType.Text,
                ShowInChart = false
            };
            db.EvolutionTemplateFields.AddRange(pain, flexion, notes);
            await db.SaveChangesAsync();

            db.PatientEvolutions.AddRange(
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = patient.Id,
                    ProfessionalId = admin.Id,
                    Date = now.AddHours(-5),
                    Status = EvolutionStatus.Completed,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = pain.Id, ValueNumber = 8 },
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = flexion.Id, ValueNumber = 90 }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = patient.Id,
                    ProfessionalId = admin.Id,
                    Date = now.AddHours(-4),
                    Status = EvolutionStatus.Draft,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = pain.Id, ValueNumber = 1 },
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = flexion.Id, ValueNumber = 100 }
                    ]
                },
                new PatientEvolution
                {
                    ClinicaId = clinic.Id,
                    PatientId = patient.Id,
                    ProfessionalId = admin.Id,
                    Date = now.AddHours(-2),
                    Status = EvolutionStatus.Completed,
                    Values =
                    [
                        new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = pain.Id, ValueNumber = 4 }
                    ]
                });
            await db.SaveChangesAsync();

            ids.ClinicaId = clinic.Id;
            ids.PatientId = patient.Id;
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin@test.local");

        var response = await client.GetAsync($"/api/patients/{ids.PatientId}/measurements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var series = await response.Content.ReadFromJsonAsync<List<MeasurementSeriesResponse>>();

        Assert.NotNull(series);
        Assert.Equal(2, series.Count);
        Assert.Equal("Escala de dor", series[0].TemplateName);
        Assert.Equal(2, series[0].EvolutionCount);
        AssertClose(now.AddHours(-2), series[0].LastDate);

        var painChart = Assert.Single(series[0].Fields);
        Assert.Equal("Dor", painChart.Label);
        Assert.Equal("Decrease", painChart.Direction);
        Assert.Equal(8, painChart.InitialValue);
        Assert.Equal(4, painChart.CurrentValue);
        Assert.Equal(50, painChart.Progress);
        Assert.Equal(2, painChart.Data.Count);
        Assert.True(painChart.Data[0].Date < painChart.Data[1].Date);
        Assert.Equal(8, painChart.Data[0].Value);
        Assert.Equal(4, painChart.Data[1].Value);

        Assert.Equal("Amplitude", series[1].TemplateName);
        Assert.Equal(1, series[1].EvolutionCount);
        AssertClose(now.AddHours(-5), series[1].LastDate);

        var mobilityChart = Assert.Single(series[1].Fields);
        Assert.Equal("Flexão", mobilityChart.Label);
        Assert.Equal("Increase", mobilityChart.Direction);
        Assert.Single(mobilityChart.Data);
        Assert.Equal(90, mobilityChart.Data[0].Value);
    }

    [Fact]
    public async Task Patient_without_measurements_returns_empty_list()
    {
        await using var app = new MultiClinicaFactory();
        var ids = new RecordIds();

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(NewUser(clinic.Id, "Admin", "admin@test.local", UserRole.Administrador));
            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente A" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            ids.PatientId = patient.Id;
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin@test.local");

        var response = await client.GetAsync($"/api/patients/{ids.PatientId}/measurements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var series = await response.Content.ReadFromJsonAsync<List<MeasurementSeriesResponse>>();
        Assert.NotNull(series);
        Assert.Empty(series);
    }

    [Fact]
    public async Task Reception_cannot_read_measurements()
    {
        await using var app = new MultiClinicaFactory();
        var ids = new RecordIds();

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(NewUser(clinic.Id, "Recepção", "recepcao@test.local", UserRole.Recepcao));
            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente A" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            ids.PatientId = patient.Id;
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "recepcao@test.local");

        var response = await client.GetAsync($"/api/patients/{ids.PatientId}/measurements");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Timeline_mixes_appointments_evolutions_and_anamneses_in_descending_order_and_paginates()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;
        var ids = new RecordIds();
        var anamnesisDate = now.AddHours(-2);

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin@test.local", UserRole.Administrador);
            var professional = NewUser(clinic.Id, "Dra. Ana", "ana@test.local", UserRole.Profissional);
            db.Users.AddRange(admin, professional);

            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente A" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var template = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Escala de dor" };
            db.EvolutionTemplates.Add(template);
            await db.SaveChangesAsync();

            var field = new EvolutionTemplateField
            {
                ClinicaId = clinic.Id,
                TemplateId = template.Id,
                Label = "Dor",
                Key = "dor",
                Type = EvolutionFieldType.Scale,
                ExpectedDirection = EvolutionDirection.Decrease,
                ShowInChart = true
            };
            db.EvolutionTemplateFields.Add(field);
            await db.SaveChangesAsync();

            var olderAppointment = new Appointment
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                PatientId = patient.Id,
                AppointmentDate = now.AddHours(-5),
                Status = AppointmentStatus.Completed
            };
            var newerAppointment = new Appointment
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                PatientId = patient.Id,
                AppointmentDate = now.AddHours(-1),
                Status = AppointmentStatus.Scheduled
            };
            db.Appointments.AddRange(olderAppointment, newerAppointment);
            await db.SaveChangesAsync();

            db.ConsultationReports.Add(new ConsultationReport
            {
                ClinicaId = clinic.Id,
                AppointmentId = olderAppointment.Id,
                PatientId = patient.Id,
                ProfessionalId = professional.Id,
                RecordedByUserId = professional.Id,
                ConsentConfirmedAt = now.AddHours(-5),
                Status = ConsultationStatus.Ready,
                StartedAt = now.AddHours(-5)
            });

            db.PatientEvolutions.Add(new PatientEvolution
            {
                ClinicaId = clinic.Id,
                PatientId = patient.Id,
                AppointmentId = olderAppointment.Id,
                ProfessionalId = professional.Id,
                Date = now.AddHours(-3),
                Status = EvolutionStatus.Completed,
                Values =
                [
                    new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = field.Id, ValueNumber = 4 }
                ]
            });

            var anamnesis = new MedicalRecord
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                PatientId = patient.Id,
                Titulo = "Avaliação inicial"
            };
            db.MedicalRecords.Add(anamnesis);
            await db.SaveChangesAsync();

            // CreatedAt é definido pelo SaveChanges; ajusta para a data do evento na linha do tempo.
            anamnesis.CreatedAt = anamnesisDate;
            await db.SaveChangesAsync();

            ids.PatientId = patient.Id;
            ids.AppointmentId = olderAppointment.Id;
            ids.NewerAppointmentId = newerAppointment.Id;
            ids.EvolutionId = await db.PatientEvolutions.Select(e => e.Id).SingleAsync();
            ids.AnamnesisId = anamnesis.Id;
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin@test.local");

        var firstPageResponse = await client.GetAsync($"/api/patients/{ids.PatientId}/timeline?take=2");
        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);

        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<TimelineResponse>();
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Events.Count);

        var newerAppointmentEvent = firstPage.Events[0];
        Assert.Equal("appointment", newerAppointmentEvent.Type);
        Assert.Equal("Consulta", newerAppointmentEvent.Title);
        Assert.Equal(ids.NewerAppointmentId, newerAppointmentEvent.Id);
        Assert.Equal("Scheduled", newerAppointmentEvent.Status);
        Assert.Null(newerAppointmentEvent.ConsultationStatus);
        Assert.Equal(0, newerAppointmentEvent.EvolutionCount);
        Assert.Equal("Dra. Ana", newerAppointmentEvent.ProfessionalName);

        var anamnesisEvent = firstPage.Events[1];
        Assert.Equal("anamnesis", anamnesisEvent.Type);
        Assert.Equal("Avaliação inicial", anamnesisEvent.Title);
        Assert.Equal(ids.AnamnesisId, anamnesisEvent.Id);
        Assert.Equal("Dra. Ana", anamnesisEvent.ProfessionalName);
        Assert.Null(anamnesisEvent.Status);
        AssertClose(anamnesisDate, anamnesisEvent.Date);

        Assert.NotNull(firstPage.NextBefore);
        AssertClose(anamnesisDate, firstPage.NextBefore.Value);

        var before = Uri.EscapeDataString(firstPage.NextBefore.Value.ToString("O"));
        var secondPageResponse = await client.GetAsync($"/api/patients/{ids.PatientId}/timeline?before={before}&take=2");
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);

        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<TimelineResponse>();
        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Events.Count);
        Assert.Null(secondPage.NextBefore);

        var evolutionEvent = secondPage.Events[0];
        Assert.Equal("evolution", evolutionEvent.Type);
        Assert.Equal("Evolução", evolutionEvent.Title);
        Assert.Equal(ids.EvolutionId, evolutionEvent.Id);
        Assert.Equal("Completed", evolutionEvent.Status);
        Assert.Equal(ids.AppointmentId, evolutionEvent.AppointmentId);
        Assert.Equal("Dra. Ana", evolutionEvent.ProfessionalName);
        Assert.Equal(["Escala de dor"], evolutionEvent.TemplateNames);

        var olderAppointmentEvent = secondPage.Events[1];
        Assert.Equal("appointment", olderAppointmentEvent.Type);
        Assert.Equal(ids.AppointmentId, olderAppointmentEvent.Id);
        Assert.Equal("Completed", olderAppointmentEvent.Status);
        Assert.Equal("Ready", olderAppointmentEvent.ConsultationStatus);
        Assert.Equal(1, olderAppointmentEvent.EvolutionCount);
    }

    [Fact]
    public async Task Timeline_for_reception_has_only_appointments_without_clinical_data()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;
        var ids = new RecordIds();

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin@test.local", UserRole.Administrador);
            var professional = NewUser(clinic.Id, "Dra. Ana", "ana@test.local", UserRole.Profissional);
            var reception = NewUser(clinic.Id, "Recepção", "recepcao@test.local", UserRole.Recepcao);
            db.Users.AddRange(admin, professional, reception);

            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente A" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var template = new EvolutionTemplate { ClinicaId = clinic.Id, Name = "Escala de dor" };
            db.EvolutionTemplates.Add(template);
            await db.SaveChangesAsync();

            var field = new EvolutionTemplateField
            {
                ClinicaId = clinic.Id,
                TemplateId = template.Id,
                Label = "Dor",
                Key = "dor",
                Type = EvolutionFieldType.Scale,
                ShowInChart = true
            };
            db.EvolutionTemplateFields.Add(field);

            var appointment = new Appointment
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                PatientId = patient.Id,
                AppointmentDate = now.AddHours(-4),
                Status = AppointmentStatus.Completed
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            db.ConsultationReports.Add(new ConsultationReport
            {
                ClinicaId = clinic.Id,
                AppointmentId = appointment.Id,
                PatientId = patient.Id,
                ProfessionalId = professional.Id,
                RecordedByUserId = professional.Id,
                ConsentConfirmedAt = now.AddHours(-4),
                Status = ConsultationStatus.Ready,
                StartedAt = now.AddHours(-4)
            });

            db.PatientEvolutions.Add(new PatientEvolution
            {
                ClinicaId = clinic.Id,
                PatientId = patient.Id,
                AppointmentId = appointment.Id,
                ProfessionalId = professional.Id,
                Date = now.AddHours(-3),
                Status = EvolutionStatus.Completed,
                Values =
                [
                    new PatientEvolutionValue { ClinicaId = clinic.Id, FieldId = field.Id, ValueNumber = 4 }
                ]
            });

            db.MedicalRecords.Add(new MedicalRecord
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                PatientId = patient.Id,
                Titulo = "Avaliação inicial"
            });
            await db.SaveChangesAsync();

            ids.PatientId = patient.Id;
            ids.AppointmentId = appointment.Id;
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "recepcao@test.local");

        var response = await client.GetAsync($"/api/patients/{ids.PatientId}/timeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var timeline = await response.Content.ReadFromJsonAsync<TimelineResponse>();
        Assert.NotNull(timeline);

        var appointmentEvent = Assert.Single(timeline.Events);
        Assert.Equal("appointment", appointmentEvent.Type);
        Assert.Equal(ids.AppointmentId, appointmentEvent.Id);
        Assert.Null(appointmentEvent.ConsultationStatus);
        Assert.Equal(0, appointmentEvent.EvolutionCount);
        Assert.Empty(appointmentEvent.TemplateNames);
    }

    [Fact]
    public async Task Patient_from_another_clinic_returns_404()
    {
        await using var app = new MultiClinicaFactory();
        var ids = new RecordIds();

        await app.SeedAsync(async db =>
        {
            var clinicA = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            var clinicB = new Clinica { Nome = "Clínica B", NomeResponsavel = "Victor" };
            db.Clinicas.AddRange(clinicA, clinicB);
            await db.SaveChangesAsync();

            db.Users.Add(NewUser(clinicA.Id, "Admin A", "admin-a@test.local", UserRole.Administrador));
            var patientB = new Patient { ClinicaId = clinicB.Id, Name = "Paciente B" };
            db.Patients.Add(patientB);
            await db.SaveChangesAsync();

            ids.PatientId = patientB.Id;
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local");

        var measurements = await client.GetAsync($"/api/patients/{ids.PatientId}/measurements");
        Assert.Equal(HttpStatusCode.NotFound, measurements.StatusCode);
        var measurementsBody = await measurements.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(measurementsBody);
        Assert.Equal("NOT_FOUND", measurementsBody.Code);

        var timeline = await client.GetAsync($"/api/patients/{ids.PatientId}/timeline");
        Assert.Equal(HttpStatusCode.NotFound, timeline.StatusCode);
        var timelineBody = await timeline.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(timelineBody);
        Assert.Equal("NOT_FOUND", timelineBody.Code);
    }

    private static User NewUser(int clinicId, string name, string email, UserRole role) => new()
    {
        ClinicaId = clinicId,
        Name = name,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        Role = role
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

    private static void AssertClose(DateTime expected, DateTime actual)
        => Assert.True(Math.Abs((expected - actual).TotalSeconds) < 1,
            $"Esperado {expected:O}, recebido {actual:O}.");

    private sealed class RecordIds
    {
        public int ClinicaId { get; set; }
        public int PatientId { get; set; }
        public int AppointmentId { get; set; }
        public int NewerAppointmentId { get; set; }
        public int EvolutionId { get; set; }
        public int AnamnesisId { get; set; }
    }

    private sealed class MeasurementSeriesResponse
    {
        public int TemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public DateTime LastDate { get; set; }
        public int EvolutionCount { get; set; }
        public List<ChartResponse> Fields { get; set; } = [];
    }

    private sealed class ChartResponse
    {
        public int FieldId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public decimal InitialValue { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal? Progress { get; set; }
        public List<PointResponse> Data { get; set; } = [];
    }

    private sealed class PointResponse
    {
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
    }

    private sealed class TimelineResponse
    {
        public List<TimelineEventResponse> Events { get; set; } = [];
        public DateTime? NextBefore { get; set; }
    }

    private sealed class TimelineEventResponse
    {
        public string Type { get; set; } = string.Empty;
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ProfessionalName { get; set; }
        public string? Status { get; set; }
        public string? ConsultationStatus { get; set; }
        public int? AppointmentId { get; set; }
        public int EvolutionCount { get; set; }
        public List<string> TemplateNames { get; set; } = [];
    }

    private sealed class ErrorResponse
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}
