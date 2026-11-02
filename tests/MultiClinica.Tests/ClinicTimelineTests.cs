using System.Net;
using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class ClinicTimelineTests
{
    private const string Password = "secret123";

    [Fact]
    public async Task Clinic_timeline_returns_events_from_two_patients_with_their_names()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;
        var patientAId = 0;
        var patientBId = 0;
        var appointmentAId = 0;
        var appointmentBId = 0;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Timeline", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin.timeline@test.local", UserRole.Administrador);
            var professional = NewUser(clinic.Id, "Dra. Ana", "ana.timeline@test.local", UserRole.Profissional);
            db.Users.AddRange(admin, professional);

            var patientA = new Patient { ClinicaId = clinic.Id, Name = "Ana Souza" };
            var patientB = new Patient { ClinicaId = clinic.Id, Name = "Bruno Lima" };
            db.Patients.AddRange(patientA, patientB);
            await db.SaveChangesAsync();

            var appointmentA = new Appointment
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                PatientId = patientA.Id,
                AppointmentDate = now.AddHours(-1),
                Status = AppointmentStatus.Completed,
            };
            var appointmentB = new Appointment
            {
                ClinicaId = clinic.Id,
                UserId = professional.Id,
                PatientId = patientB.Id,
                AppointmentDate = now.AddHours(-2),
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.AddRange(appointmentA, appointmentB);
            await db.SaveChangesAsync();

            patientAId = patientA.Id;
            patientBId = patientB.Id;
            appointmentAId = appointmentA.Id;
            appointmentBId = appointmentB.Id;
        });

        using var client = await LoginAsync(app, "admin.timeline@test.local");

        var timeline = await client.GetFromJsonAsync<ClinicTimelineResponse>("/api/patient-records/timeline");

        Assert.NotNull(timeline);
        Assert.Equal(2, timeline.Events.Count);

        var fromA = Assert.Single(timeline.Events, e => e.PatientId == patientAId);
        Assert.Equal("Ana Souza", fromA.PatientName);
        Assert.Equal("appointment", fromA.Event.Type);
        Assert.Equal(appointmentAId, fromA.Event.Id);
        Assert.Equal("Consulta", fromA.Event.Title);
        Assert.Equal("Completed", fromA.Event.Status);

        var fromB = Assert.Single(timeline.Events, e => e.PatientId == patientBId);
        Assert.Equal("Bruno Lima", fromB.PatientName);
        Assert.Equal("appointment", fromB.Event.Type);
        Assert.Equal(appointmentBId, fromB.Event.Id);
        Assert.Equal("Scheduled", fromB.Event.Status);

        // Ordem global decrescente por data.
        Assert.Equal(fromA.Event.Id, timeline.Events[0].Event.Id);
        Assert.Null(timeline.NextBefore);
    }

    [Fact]
    public async Task Clinic_timeline_does_not_include_events_from_another_clinic()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;
        var patientAId = 0;

        await app.SeedAsync(async db =>
        {
            var clinicA = new Clinica { Nome = "Clínica A", NomeResponsavel = "Victor" };
            var clinicB = new Clinica { Nome = "Clínica B", NomeResponsavel = "Victor" };
            db.Clinicas.AddRange(clinicA, clinicB);
            await db.SaveChangesAsync();

            var adminA = NewUser(clinicA.Id, "Admin A", "admin.a@test.local", UserRole.Administrador);
            var professionalB = NewUser(clinicB.Id, "Prof. B", "prof.b@test.local", UserRole.Profissional);
            db.Users.AddRange(adminA, professionalB);

            var patientA = new Patient { ClinicaId = clinicA.Id, Name = "Paciente A" };
            var patientB = new Patient { ClinicaId = clinicB.Id, Name = "Paciente B" };
            db.Patients.AddRange(patientA, patientB);
            await db.SaveChangesAsync();

            db.Appointments.AddRange(
                new Appointment
                {
                    ClinicaId = clinicA.Id,
                    UserId = adminA.Id,
                    PatientId = patientA.Id,
                    AppointmentDate = now.AddHours(-1),
                },
                new Appointment
                {
                    ClinicaId = clinicB.Id,
                    UserId = professionalB.Id,
                    PatientId = patientB.Id,
                    AppointmentDate = now.AddHours(-2),
                });
            await db.SaveChangesAsync();

            patientAId = patientA.Id;
        });

        using var client = await LoginAsync(app, "admin.a@test.local");

        var timeline = await client.GetFromJsonAsync<ClinicTimelineResponse>("/api/patient-records/timeline");

        Assert.NotNull(timeline);
        var single = Assert.Single(timeline.Events);
        Assert.Equal(patientAId, single.PatientId);
        Assert.Equal("Paciente A", single.PatientName);
    }

    [Fact]
    public async Task Clinic_timeline_first_page_starts_at_today_and_hides_future_appointments()
    {
        await using var app = new MultiClinicaFactory();
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var localToday = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        var yesterday = TimeZoneInfo.ConvertTimeToUtc(localToday.AddDays(-1).AddHours(10), timeZone);
        var tomorrow = TimeZoneInfo.ConvertTimeToUtc(localToday.AddDays(1).AddHours(10), timeZone);
        var yesterdayId = 0;
        var tomorrowId = 0;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Hoje", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin.today@test.local", UserRole.Administrador);
            db.Users.Add(admin);

            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente Hoje" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var past = new Appointment
            {
                ClinicaId = clinic.Id,
                UserId = admin.Id,
                PatientId = patient.Id,
                AppointmentDate = yesterday,
                Status = AppointmentStatus.Completed,
            };
            var future = new Appointment
            {
                ClinicaId = clinic.Id,
                UserId = admin.Id,
                PatientId = patient.Id,
                AppointmentDate = tomorrow,
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.AddRange(past, future);
            await db.SaveChangesAsync();

            yesterdayId = past.Id;
            tomorrowId = future.Id;
        });

        using var client = await LoginAsync(app, "admin.today@test.local");

        var firstPage = await client.GetFromJsonAsync<ClinicTimelineResponse>("/api/patient-records/timeline");

        Assert.NotNull(firstPage);
        var only = Assert.Single(firstPage.Events);
        Assert.Equal(yesterdayId, only.Event.Id);
        Assert.DoesNotContain(firstPage.Events, entry => entry.Event.Id == tomorrowId);
        Assert.Null(firstPage.NextBefore);
    }

    [Fact]
    public async Task Reception_cannot_read_the_clinic_timeline()
    {
        await using var app = new MultiClinicaFactory();

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Recepção", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(NewUser(clinic.Id, "Recepção", "recepcao.timeline@test.local", UserRole.Recepcao));
            await db.SaveChangesAsync();
        });

        using var client = await LoginAsync(app, "recepcao.timeline@test.local");

        var response = await client.GetAsync("/api/patient-records/timeline");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clinic_timeline_paginates_with_take_and_second_page_does_not_repeat_events()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Páginas", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin.pages@test.local", UserRole.Administrador);
            db.Users.Add(admin);

            var patientA = new Patient { ClinicaId = clinic.Id, Name = "Paciente A" };
            var patientB = new Patient { ClinicaId = clinic.Id, Name = "Paciente B" };
            db.Patients.AddRange(patientA, patientB);
            await db.SaveChangesAsync();

            db.Appointments.AddRange(
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = admin.Id,
                    PatientId = patientA.Id,
                    AppointmentDate = now.AddHours(-1),
                },
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = admin.Id,
                    PatientId = patientB.Id,
                    AppointmentDate = now.AddHours(-2),
                },
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = admin.Id,
                    PatientId = patientA.Id,
                    AppointmentDate = now.AddHours(-3),
                });
            await db.SaveChangesAsync();
        });

        using var client = await LoginAsync(app, "admin.pages@test.local");

        var firstPage = await client.GetFromJsonAsync<ClinicTimelineResponse>("/api/patient-records/timeline?take=2");
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage.Events.Count);
        Assert.NotNull(firstPage.NextBefore);

        var firstKeys = firstPage.Events.Select(EventKey).ToList();

        var before = Uri.EscapeDataString(firstPage.NextBefore.Value.ToString("O"));
        var secondPage = await client.GetFromJsonAsync<ClinicTimelineResponse>(
            $"/api/patient-records/timeline?before={before}&take=2");
        Assert.NotNull(secondPage);
        var single = Assert.Single(secondPage.Events);
        Assert.Null(secondPage.NextBefore);

        var allKeys = firstKeys.Append(EventKey(single)).ToList();
        Assert.Equal(3, allKeys.Count);
        Assert.Equal(3, allKeys.Distinct().Count());
        Assert.DoesNotContain(EventKey(single), firstKeys);
    }

    [Fact]
    public async Task Clinic_timeline_hides_events_of_deleted_patients()
    {
        await using var app = new MultiClinicaFactory();
        var activeId = 0;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Excluídos", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin.deleted@test.local", UserRole.Administrador);
            db.Users.Add(admin);
            var active = new Patient { ClinicaId = clinic.Id, Name = "Ativo" };
            // Só anamnese: a exclusão é permitida porque ela não olha prontuários.
            var deleted = new Patient { ClinicaId = clinic.Id, Name = "Excluído", IsDeleted = true };
            db.Patients.AddRange(active, deleted);
            await db.SaveChangesAsync();

            db.MedicalRecords.AddRange(
                new MedicalRecord { ClinicaId = clinic.Id, UserId = admin.Id, PatientId = active.Id, Titulo = "Anamnese" },
                new MedicalRecord { ClinicaId = clinic.Id, UserId = admin.Id, PatientId = deleted.Id, Titulo = "Anamnese" });
            await db.SaveChangesAsync();
            activeId = active.Id;
        });

        using var client = await LoginAsync(app, "admin.deleted@test.local");
        var timeline = await client.GetFromJsonAsync<ClinicTimelineResponse>("/api/patient-records/timeline");

        Assert.NotNull(timeline);
        var only = Assert.Single(timeline.Events);
        Assert.Equal(activeId, only.PatientId);
    }

    private static string EventKey(ClinicTimelineEventResponse entry)
        => $"{entry.PatientId}:{entry.Event.Type}:{entry.Event.Id}";

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

    private sealed class ClinicTimelineResponse
    {
        public List<ClinicTimelineEventResponse> Events { get; set; } = [];
        public DateTime? NextBefore { get; set; }
    }

    private sealed class ClinicTimelineEventResponse
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public TimelineEventResponse Event { get; set; } = new();
    }

    private sealed class TimelineEventResponse
    {
        public string Type { get; set; } = string.Empty;
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Status { get; set; }
    }
}
