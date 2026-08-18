using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class AppointmentRequestTests
{
    private const string PatientPassword = "senha-super-forte";
    private const string AdminPassword = "secret123";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed record RequestDto(int Id, int PatientAccountId, int ClinicId, string? ClinicName,
        DateTime RequestedDate, string? Reason, AppointmentRequestStatus Status, string? ResponseReason,
        CancellationOrigin? CancelledBy, DateTime? RespondedAt, int? AppointmentId);

    private sealed class Scenario
    {
        public int ClinicId;
        public int ProfessionalId;
        public int AccountId;
        public string AdminEmail = "";
        public string PatientEmail = "";
    }

    // ── Seeding ──────────────────────────────────────────────────────────────

    private static async Task<Scenario> SeedAsync(MultiClinicaFactory app, string suffix, bool acceptsRequests = true)
    {
        var s = new Scenario
        {
            AdminEmail = $"admin-{suffix}@test.local",
            PatientEmail = $"patient-{suffix}@example.com"
        };

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = $"Clinica {suffix}", NomeFantasia = $"Clinica {suffix}", NomeResponsavel = "Victor",
                Email = $"clinic-{suffix}@test.local", AcceptsAppointmentRequests = acceptsRequests
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            s.ClinicId = clinic.Id;

            db.Users.Add(new User
            {
                ClinicaId = clinic.Id, Name = "Admin", Email = s.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword), Role = UserRole.Administrador
            });
            var professional = new User
            {
                ClinicaId = clinic.Id, Name = "Dr. Prof", Email = $"prof-{suffix}@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(AdminPassword), Role = UserRole.Profissional
            };
            db.Users.Add(professional);

            var account = new PatientAccount
            {
                Name = "Paciente", Email = s.PatientEmail, CPF = $"1111111{suffix.PadLeft(4, '0')}",
                Status = PatientAccountStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(PatientPassword), ActivatedAt = DateTime.UtcNow
            };
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            s.ProfessionalId = professional.Id;
            s.AccountId = account.Id;

            db.Patients.Add(new Patient { ClinicaId = clinic.Id, PatientAccountId = account.Id, Name = "Paciente" });
            await db.SaveChangesAsync();
        });

        return s;
    }

    private static async Task<HttpClient> LoginPatientAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new { email, password = PatientPassword })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<HttpClient> LoginClinicAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = AdminPassword })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<int> CreateRequestAsync(HttpClient patient, int clinicId)
    {
        var response = await patient.PostAsJsonAsync("/api/patient/appointment-requests",
            new { clinicId, requestedDate = DateTime.UtcNow.AddDays(3), reason = "Avaliação" });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<RequestDto>(Json);
        return dto!.Id;
    }

    // ── Testes ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Patient_creates_request_as_pending()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1");
        using var patient = await LoginPatientAsync(app, s.PatientEmail);

        var response = await patient.PostAsJsonAsync("/api/patient/appointment-requests",
            new { clinicId = s.ClinicId, requestedDate = DateTime.UtcNow.AddDays(3), reason = "Avaliação" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequestDto>(Json);
        Assert.Equal(AppointmentRequestStatus.Pending, dto!.Status);
        Assert.Null(dto.AppointmentId);
    }

    [Fact]
    public async Task Clinic_with_requests_disabled_is_rejected()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1", acceptsRequests: false);
        using var patient = await LoginPatientAsync(app, s.PatientEmail);

        var response = await patient.PostAsJsonAsync("/api/patient/appointment-requests",
            new { clinicId = s.ClinicId, requestedDate = DateTime.UtcNow.AddDays(3), reason = "x" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Patient_lists_only_own_requests()
    {
        await using var app = new MultiClinicaFactory();
        var s1 = await SeedAsync(app, "1");
        var s2 = await SeedAsync(app, "2");
        using var p1 = await LoginPatientAsync(app, s1.PatientEmail);
        using var p2 = await LoginPatientAsync(app, s2.PatientEmail);
        await CreateRequestAsync(p1, s1.ClinicId);
        await CreateRequestAsync(p2, s2.ClinicId);

        var list = await p1.GetFromJsonAsync<List<RequestDto>>("/api/patient/appointment-requests", Json);
        Assert.Single(list!);
        Assert.Equal(s1.AccountId, list![0].PatientAccountId);
    }

    [Fact]
    public async Task Clinic_lists_only_its_own_tenant_requests()
    {
        await using var app = new MultiClinicaFactory();
        var s1 = await SeedAsync(app, "1");
        var s2 = await SeedAsync(app, "2");
        using var p1 = await LoginPatientAsync(app, s1.PatientEmail);
        using var p2 = await LoginPatientAsync(app, s2.PatientEmail);
        await CreateRequestAsync(p1, s1.ClinicId);
        await CreateRequestAsync(p2, s2.ClinicId);

        using var clinic1 = await LoginClinicAsync(app, s1.AdminEmail);
        var list = await clinic1.GetFromJsonAsync<List<RequestDto>>("/api/appointment-requests", Json);

        Assert.Single(list!);
        Assert.Equal(s1.ClinicId, list![0].ClinicId);
    }

    [Fact]
    public async Task Patient_cancels_pending_request()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1");
        using var patient = await LoginPatientAsync(app, s.PatientEmail);
        var id = await CreateRequestAsync(patient, s.ClinicId);

        var response = await patient.PatchAsJsonAsync($"/api/patient/appointment-requests/{id}/cancel",
            new { reason = "Mudei de ideia" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequestDto>(Json);
        Assert.Equal(AppointmentRequestStatus.Cancelled, dto!.Status);
        Assert.Equal(CancellationOrigin.Patient, dto.CancelledBy);
    }

    [Fact]
    public async Task Clinic_rejects_with_reason()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1");
        using var patient = await LoginPatientAsync(app, s.PatientEmail);
        var id = await CreateRequestAsync(patient, s.ClinicId);

        using var clinic = await LoginClinicAsync(app, s.AdminEmail);
        var noReason = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/reject", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode); // motivo obrigatório

        var response = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/reject",
            new { reason = "Sem disponibilidade" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequestDto>(Json);
        Assert.Equal(AppointmentRequestStatus.Rejected, dto!.Status);
        Assert.Equal("Sem disponibilidade", dto.ResponseReason);
    }

    [Fact]
    public async Task Clinic_cancels_with_reason_and_origin()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1");
        using var patient = await LoginPatientAsync(app, s.PatientEmail);
        var id = await CreateRequestAsync(patient, s.ClinicId);

        using var clinic = await LoginClinicAsync(app, s.AdminEmail);
        var response = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/cancel",
            new { reason = "Agenda fechada" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequestDto>(Json);
        Assert.Equal(AppointmentRequestStatus.Cancelled, dto!.Status);
        Assert.Equal(CancellationOrigin.Clinic, dto.CancelledBy);
    }

    [Fact]
    public async Task Clinic_accepts_and_creates_appointment()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1");
        using var patient = await LoginPatientAsync(app, s.PatientEmail);
        var id = await CreateRequestAsync(patient, s.ClinicId);

        using var clinic = await LoginClinicAsync(app, s.AdminEmail);
        var response = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/accept",
            new { professionalId = s.ProfessionalId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<RequestDto>(Json);
        Assert.Equal(AppointmentRequestStatus.Accepted, dto!.Status);
        Assert.NotNull(dto.AppointmentId);

        await app.SeedAsync(async db =>
        {
            var appt = await db.Appointments.SingleAsync(a => a.Id == dto.AppointmentId);
            Assert.Equal(s.ClinicId, appt.ClinicaId);
            Assert.Equal(s.ProfessionalId, appt.UserId);
        });
    }

    [Fact]
    public async Task Accepted_request_cannot_be_accepted_twice()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1");
        using var patient = await LoginPatientAsync(app, s.PatientEmail);
        var id = await CreateRequestAsync(patient, s.ClinicId);

        using var clinic = await LoginClinicAsync(app, s.AdminEmail);
        var first = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/accept", new { professionalId = s.ProfessionalId });
        first.EnsureSuccessStatusCode();
        var second = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/accept", new { professionalId = s.ProfessionalId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await app.SeedAsync(async db => Assert.Equal(1, await db.Appointments.CountAsync())); // não duplica
    }

    [Fact]
    public async Task Accepted_request_cannot_be_cancelled_as_request()
    {
        await using var app = new MultiClinicaFactory();
        var s = await SeedAsync(app, "1");
        using var patient = await LoginPatientAsync(app, s.PatientEmail);
        var id = await CreateRequestAsync(patient, s.ClinicId);

        using var clinic = await LoginClinicAsync(app, s.AdminEmail);
        (await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/accept", new { professionalId = s.ProfessionalId })).EnsureSuccessStatusCode();

        var patientCancel = await patient.PatchAsJsonAsync($"/api/patient/appointment-requests/{id}/cancel", new { reason = "x" });
        var clinicCancel = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/cancel", new { reason = "x" });

        Assert.Equal(HttpStatusCode.Conflict, patientCancel.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, clinicCancel.StatusCode);
    }

    [Fact]
    public async Task Professional_from_another_clinic_is_rejected_and_request_stays_pending()
    {
        await using var app = new MultiClinicaFactory();
        var s1 = await SeedAsync(app, "1");
        var s2 = await SeedAsync(app, "2");
        using var patient = await LoginPatientAsync(app, s1.PatientEmail);
        var id = await CreateRequestAsync(patient, s1.ClinicId);

        using var clinic = await LoginClinicAsync(app, s1.AdminEmail);
        var response = await clinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/accept",
            new { professionalId = s2.ProfessionalId }); // profissional de outra clínica

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await app.SeedAsync(async db =>
        {
            var request = await db.AppointmentRequests.SingleAsync(r => r.Id == id);
            Assert.Equal(AppointmentRequestStatus.Pending, request.Status); // transação não deixou estado parcial
            Assert.Null(request.AppointmentId);
            Assert.Equal(0, await db.Appointments.CountAsync());
        });
    }

    [Fact]
    public async Task Clinic_cannot_operate_request_of_another_tenant()
    {
        await using var app = new MultiClinicaFactory();
        var s1 = await SeedAsync(app, "1");
        var s2 = await SeedAsync(app, "2");
        using var patient = await LoginPatientAsync(app, s1.PatientEmail);
        var id = await CreateRequestAsync(patient, s1.ClinicId);

        using var otherClinic = await LoginClinicAsync(app, s2.AdminEmail);
        var get = await otherClinic.GetAsync($"/api/appointment-requests/{id}");
        var accept = await otherClinic.PatchAsJsonAsync($"/api/appointment-requests/{id}/accept", new { professionalId = s2.ProfessionalId });

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
    }
}
