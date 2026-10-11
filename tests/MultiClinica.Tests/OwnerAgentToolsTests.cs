using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using Xunit;

namespace MultiClinica.Tests;

public class OwnerAgentToolsTests
{
    private static readonly TimeZoneInfo Sp = AgentTime.FindZone("America/Sao_Paulo");

    // Data local futura e estável: 3 dias à frente, 09:00 em São Paulo.
    private static string FutureLocal(int hour = 9)
    {
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Sp).Date;
        return localToday.AddDays(3).AddHours(hour).ToString("yyyy-MM-ddTHH:mm");
    }

    private static async Task<(MultiClinicaFactory App, int ClinicId, int AdminId, int PatientId)> SetupAsync()
    {
        var app = new MultiClinicaFactory();
        var (clinicId, adminId) = await AgentSeed.ClinicWithAdminAsync(app);
        var patientId = 0;
        await app.SeedAsync(async db =>
        {
            var other = new Clinica { Nome = "Outra", Email = "outra@test.local" };
            db.Clinicas.Add(other);
            var patient = new Patient { ClinicaId = clinicId, Name = "Maria Aparecida", Phone = "11999998888" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            db.Patients.Add(new Patient { ClinicaId = other.Id, Name = "Aparecida de Outra Clínica" });
            await db.SaveChangesAsync();
            patientId = patient.Id;
        });
        return (app, clinicId, adminId, patientId);
    }

    private static async Task<JsonElement> RunAsync(MultiClinicaFactory app, int clinicId, int adminId,
        string tool, object args)
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AgentIdentity>().Set(clinicId, adminId, UserRole.Administrador);
        var tools = scope.ServiceProvider.GetRequiredService<OwnerAgentTools>();
        var json = await tools.ExecuteAsync(tool, JsonSerializer.Serialize(args));
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task Search_is_case_insensitive_and_scoped_to_clinic()
    {
        var (app, clinicId, adminId, patientId) = await SetupAsync();
        await using var _ = app;
        var result = await RunAsync(app, clinicId, adminId, "buscar_paciente", new { nome = "cida" });

        Assert.True(result.GetProperty("ok").GetBoolean());
        var found = result.GetProperty("pacientes").EnumerateArray().ToList();
        Assert.Single(found);
        Assert.Equal(patientId, found[0].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Schedule_converts_local_time_to_utc()
    {
        var (app, clinicId, adminId, patientId) = await SetupAsync();
        await using var _ = app;
        var local = FutureLocal();

        var result = await RunAsync(app, clinicId, adminId, "agendar",
            new { paciente_id = patientId, profissional_id = adminId, data_hora = local });

        Assert.True(result.GetProperty("ok").GetBoolean());
        // Independente de AgentTime: São Paulo é UTC-3 (sem horário de verão desde 2019).
        var expectedUtc = DateTime.SpecifyKind(
            DateTime.ParseExact(local, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture).AddHours(3), DateTimeKind.Utc);
        await app.SeedAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync();
            Assert.Equal(expectedUtc, DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc));
        });
    }

    [Fact]
    public async Task Conflict_comes_back_as_error_for_the_model()
    {
        var (app, clinicId, adminId, patientId) = await SetupAsync();
        await using var _ = app;
        var args = new { paciente_id = patientId, profissional_id = adminId, data_hora = FutureLocal() };
        await RunAsync(app, clinicId, adminId, "agendar", args);

        var second = await RunAsync(app, clinicId, adminId, "agendar", args);

        Assert.False(second.GetProperty("ok").GetBoolean());
        Assert.Equal("Este horário já está ocupado.", second.GetProperty("erro").GetString());
    }

    [Fact]
    public async Task Reschedule_and_cancel_update_the_appointment()
    {
        var (app, clinicId, adminId, patientId) = await SetupAsync();
        await using var _ = app;
        var created = await RunAsync(app, clinicId, adminId, "agendar",
            new { paciente_id = patientId, profissional_id = adminId, data_hora = FutureLocal(9) });
        var id = created.GetProperty("consulta_id").GetInt32();

        var moved = await RunAsync(app, clinicId, adminId, "remarcar", new { consulta_id = id, nova_data_hora = FutureLocal(14) });
        Assert.True(moved.GetProperty("ok").GetBoolean());

        var cancelled = await RunAsync(app, clinicId, adminId, "cancelar", new { consulta_id = id });
        Assert.True(cancelled.GetProperty("ok").GetBoolean());
        await app.SeedAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync();
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
            Assert.Equal(AgentTime.ToUtc(FutureLocal(14), Sp), DateTime.SpecifyKind(appointment.AppointmentDate, DateTimeKind.Utc));
        });
    }

    [Fact]
    public async Task Agenda_and_free_slots_reflect_bookings()
    {
        var (app, clinicId, adminId, patientId) = await SetupAsync();
        await using var _ = app;
        var local = FutureLocal(9);
        await RunAsync(app, clinicId, adminId, "agendar",
            new { paciente_id = patientId, profissional_id = adminId, data_hora = local });
        var day = local[..10];

        var agenda = await RunAsync(app, clinicId, adminId, "ver_agenda", new { data_inicio = day, data_fim = day });
        var item = Assert.Single(agenda.GetProperty("consultas").EnumerateArray());
        Assert.Equal(local, item.GetProperty("data_hora").GetString());

        var slots = await RunAsync(app, clinicId, adminId, "consultar_horarios", new { data_inicio = day });
        var times = slots.GetProperty("profissionais")[0].GetProperty("horarios_livres").EnumerateArray()
            .Select(t => t.GetString()).ToList();
        Assert.DoesNotContain(local, times);
        Assert.Contains($"{day}T10:00", times);
    }

    [Fact]
    public async Task Free_slots_treats_null_optional_args_as_absent()
    {
        var (app, clinicId, adminId, _) = await SetupAsync();
        await using var _ = app;
        var day = FutureLocal()[..10];

        var slots = await RunAsync(app, clinicId, adminId, "consultar_horarios",
            new { data_inicio = day, profissional_id = (int?)null, data_fim = (string?)null });

        Assert.True(slots.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Cancel_refuses_appointment_that_is_not_scheduled()
    {
        var (app, clinicId, adminId, patientId) = await SetupAsync();
        await using var _ = app;
        var created = await RunAsync(app, clinicId, adminId, "agendar",
            new { paciente_id = patientId, profissional_id = adminId, data_hora = FutureLocal() });
        var id = created.GetProperty("consulta_id").GetInt32();
        await app.SeedAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync();
            appointment.Status = AppointmentStatus.Completed;
            await db.SaveChangesAsync();
        });

        var result = await RunAsync(app, clinicId, adminId, "cancelar", new { consulta_id = id });

        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Equal("Só é possível cancelar consulta agendada.", result.GetProperty("erro").GetString());
        await app.SeedAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync();
            Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        });
    }

    [Fact]
    public async Task Other_clinic_appointments_are_invisible_and_untouchable()
    {
        var (app, clinicId, adminId, _) = await SetupAsync();
        await using var _ = app;
        var local = FutureLocal(9);
        var day = local[..10];
        var foreignId = 0;
        await app.SeedAsync(async db =>
        {
            var other = await db.Clinicas.SingleAsync(c => c.Nome == "Outra");
            var professional = new User { ClinicaId = other.Id, Name = "Dra. Outra", Email = "pro-outra@test.local", Role = UserRole.Profissional };
            var patient = new Patient { ClinicaId = other.Id, Name = "Paciente Outra", Phone = "11977776666" };
            db.Users.Add(professional);
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            var appointment = new Appointment { ClinicaId = other.Id, UserId = professional.Id, PatientId = patient.Id,
                AppointmentDate = AgentTime.ToUtc(local, Sp), Status = AppointmentStatus.Scheduled };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            foreignId = appointment.Id;
        });

        var agenda = await RunAsync(app, clinicId, adminId, "ver_agenda", new { data_inicio = day, data_fim = day });
        Assert.Empty(agenda.GetProperty("consultas").EnumerateArray());

        var moved = await RunAsync(app, clinicId, adminId, "remarcar",
            new { consulta_id = foreignId, nova_data_hora = FutureLocal(14) });
        Assert.False(moved.GetProperty("ok").GetBoolean());
        var cancelled = await RunAsync(app, clinicId, adminId, "cancelar", new { consulta_id = foreignId });
        Assert.False(cancelled.GetProperty("ok").GetBoolean());
        await app.SeedAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync(a => a.Id == foreignId);
            Assert.Equal(AppointmentStatus.Scheduled, appointment.Status);
        });
    }

    [Fact]
    public async Task Bad_input_never_throws()
    {
        var (app, clinicId, adminId, _) = await SetupAsync();
        await using var __ = app;
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AgentIdentity>().Set(clinicId, adminId, UserRole.Administrador);
        var tools = scope.ServiceProvider.GetRequiredService<OwnerAgentTools>();

        foreach (var json in new[] {
            await tools.ExecuteAsync("nao_existe", "{}"),
            await tools.ExecuteAsync("agendar", "{ isso não é json"),
            await tools.ExecuteAsync("agendar", "{}"),
            await tools.ExecuteAsync("agendar", "{\"paciente_id\":1,\"profissional_id\":1,\"data_hora\":\"terça 9h\"}") })
        {
            var root = JsonDocument.Parse(json).RootElement;
            Assert.False(root.GetProperty("ok").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("erro").GetString()));
        }
    }

    [Fact]
    public async Task Argument_errors_use_exact_messages()
    {
        var (app, clinicId, adminId, _) = await SetupAsync();
        await using var _ = app;

        var missing = await RunAsync(app, clinicId, adminId, "agendar", new { });
        Assert.Equal("Parâmetro obrigatório ausente: paciente_id.", missing.GetProperty("erro").GetString());

        var invalid = await RunAsync(app, clinicId, adminId, "agendar",
            new { paciente_id = "abc", profissional_id = 1, data_hora = "2030-01-01T09:00" });
        Assert.Equal("Parâmetro inválido: paciente_id.", invalid.GetProperty("erro").GetString());
    }

    [Fact]
    public async Task Numeric_string_argument_is_parsed_and_reaches_the_service()
    {
        var (app, clinicId, adminId, _) = await SetupAsync();
        await using var _ = app;

        var result = await RunAsync(app, clinicId, adminId, "cancelar", new { consulta_id = "999999" });

        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Equal("Consulta não encontrada.", result.GetProperty("erro").GetString());
    }
}
