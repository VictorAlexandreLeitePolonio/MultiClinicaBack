using System.Net.Http.Json;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PatientCardsTests
{
    private const string Password = "secret123";

    [Fact]
    public async Task Patient_cards_expose_next_appointment_and_last_completed_session()
    {
        await using var app = new MultiClinicaFactory();
        var now = DateTime.UtcNow;
        var nextAppointment = now.AddDays(2);
        var lastSession = now.AddDays(-3);
        var patientId = 0;

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clínica Cards", NomeResponsavel = "Victor" };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            var admin = NewUser(clinic.Id, "Admin", "admin.cards@test.local", UserRole.Administrador);
            var professional = NewUser(clinic.Id, "Dra. Ana", "ana.cards@test.local", UserRole.Profissional);
            db.Users.AddRange(admin, professional);

            var patient = new Patient { ClinicaId = clinic.Id, Name = "Paciente Cards", IsActive = true };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            db.Appointments.AddRange(
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = professional.Id,
                    PatientId = patient.Id,
                    AppointmentDate = nextAppointment,
                    Status = AppointmentStatus.Scheduled,
                },
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = professional.Id,
                    PatientId = patient.Id,
                    AppointmentDate = lastSession,
                    Status = AppointmentStatus.Completed,
                },
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = professional.Id,
                    PatientId = patient.Id,
                    AppointmentDate = now.AddDays(-1),
                    Status = AppointmentStatus.Cancelled,
                },
                // Agendamento passado: não pode virar "próxima consulta".
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = professional.Id,
                    PatientId = patient.Id,
                    AppointmentDate = now.AddDays(-5),
                    Status = AppointmentStatus.Scheduled,
                },
                // Excluída: não pode aparecer em nenhum dos dois campos.
                new Appointment
                {
                    ClinicaId = clinic.Id,
                    UserId = professional.Id,
                    PatientId = patient.Id,
                    AppointmentDate = now.AddDays(6),
                    Status = AppointmentStatus.Scheduled,
                    IsDeleted = true,
                });
            await db.SaveChangesAsync();

            patientId = patient.Id;
        });

        using var client = await LoginAsync(app, "admin.cards@test.local");

        var list = await client.GetFromJsonAsync<PatientListResponse>("/api/patients");
        Assert.NotNull(list);
        var fromList = Assert.Single(list.Data);
        Assert.Equal(patientId, fromList.Id);
        AssertClose(nextAppointment, fromList.NextAppointmentAt);
        AssertClose(lastSession, fromList.LastSessionAt);

        var detail = await client.GetFromJsonAsync<PatientResponse>($"/api/patients/{patientId}");
        Assert.NotNull(detail);
        AssertClose(nextAppointment, detail.NextAppointmentAt);
        AssertClose(lastSession, detail.LastSessionAt);
    }

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

    private static void AssertClose(DateTime? expected, DateTime? actual)
    {
        Assert.NotNull(actual);
        Assert.True(Math.Abs((expected!.Value - actual!.Value).TotalSeconds) < 1,
            $"Esperado {expected.Value:O}, recebido {actual.Value:O}.");
    }

    private sealed class PatientListResponse
    {
        public List<PatientResponse> Data { get; set; } = [];
        public int TotalCount { get; set; }
    }

    private sealed class PatientResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime? NextAppointmentAt { get; set; }
        public DateTime? LastSessionAt { get; set; }
    }
}
