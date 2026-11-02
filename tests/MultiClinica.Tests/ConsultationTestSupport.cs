using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Agent;
using MultiClinica.API.Services.Transcription;

namespace MultiClinica.Tests;

public sealed class FakeTranscriber : ITranscriber
{
    public Queue<Func<TranscriptionResult>> Script { get; } = new();
    // Tamanho em bytes de cada áudio recebido.
    public List<long> ReceivedBytes { get; } = [];

    public FakeTranscriber Reply(string text, int seconds)
    {
        Script.Enqueue(() => new TranscriptionResult(text, seconds));
        return this;
    }

    public FakeTranscriber Throw()
    {
        Script.Enqueue(() => throw new HttpRequestException("Transcritor fora do ar"));
        return this;
    }

    public FakeTranscriber NotConfigured()
    {
        Script.Enqueue(() => throw new TranscriptionNotConfiguredException());
        return this;
    }

    public async Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await audio.CopyToAsync(buffer, ct);
        ReceivedBytes.Add(buffer.Length);
        if (Script.Count == 0) throw new InvalidOperationException("FakeTranscriber sem resposta roteirizada.");
        return Script.Dequeue()();
    }
}

// Cenário comum das consultas gravadas: clínica Premium, admin, dois profissionais,
// paciente, plano Avulso e um agendamento Scheduled do profissional hoje às 10h (São Paulo).
internal static class ConsultationTestSupport
{
    public const string Password = "senha-super-forte";

    public static MultiClinicaFactory CreateApp(FakeTranscriber? transcriber = null, FakeChatModel? chat = null)
    {
        var storage = Path.Combine(Path.GetTempPath(), "consult-tests", Guid.NewGuid().ToString("N"));
        return new MultiClinicaFactory
        {
            ConfigureTestServices = services =>
            {
                services.PostConfigure<TranscriptionOptions>(o => o.StoragePath = storage);
                services.PostConfigure<AgentOptions>(o => { o.ApiKey = "test-key"; o.Model = "test-model"; });
                services.RemoveAll<ITranscriber>();
                services.AddSingleton<ITranscriber>(transcriber ?? new FakeTranscriber());
                services.RemoveAll<IChatModel>();
                services.AddSingleton<IChatModel>(chat ?? new FakeChatModel());
            },
        };
    }

    public static async Task<SeededConsultation> SeedAsync(MultiClinicaFactory app, ClinicPlan plan = ClinicPlan.Premium)
    {
        var seeded = new SeededConsultation();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Clínica Consulta", NomeFantasia = "Clínica Consulta", NomeResponsavel = "Admin",
                Email = $"clinica-{suffix}@test.local", Plan = plan, TimeZoneId = "America/Sao_Paulo",
                AppointmentSlotDurationMinutes = 60,
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            User NewUser(string name, string email, UserRole role) => new()
            {
                ClinicaId = clinic.Id, Name = name, Email = email, Role = role,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            };
            var admin = NewUser("Admin", $"admin-{suffix}@test.local", UserRole.Administrador);
            var professional = NewUser("Dra. Ana", $"pro-{suffix}@test.local", UserRole.Profissional);
            var other = NewUser("Dr. Bruno", $"outro-{suffix}@test.local", UserRole.Profissional);
            var reception = NewUser("Recepção", $"recepcao-{suffix}@test.local", UserRole.Recepcao);
            db.Users.AddRange(admin, professional, other, reception);

            var patient = new Patient { ClinicaId = clinic.Id, Name = "Carlos Paciente", IsActive = true };
            db.Patients.Add(patient);
            var sessionType = new SessionType { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            db.SessionTypes.Add(sessionType);
            await db.SaveChangesAsync();

            var plans = new Plans
            {
                ClinicaId = clinic.Id, Name = "Sessão avulsa", Valor = 150m,
                TipoPlano = TipoPlano.Avulso, TipoSessaoId = sessionType.Id,
            };
            db.Plans.Add(plans);

            var saoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saoPaulo).Date;
            var appointment = new Appointment
            {
                ClinicaId = clinic.Id, UserId = professional.Id, PatientId = patient.Id,
                AppointmentDate = TimeZoneInfo.ConvertTimeToUtc(today.AddHours(10), saoPaulo),
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();

            seeded.ClinicaId = clinic.Id;
            seeded.AdminId = admin.Id;
            seeded.ProfessionalId = professional.Id;
            seeded.OtherProfessionalId = other.Id;
            seeded.PatientId = patient.Id;
            seeded.PlanId = plans.Id;
            seeded.AppointmentId = appointment.Id;
            seeded.AdminEmail = admin.Email;
            seeded.ProfessionalEmail = professional.Email;
            seeded.OtherProfessionalEmail = other.Email;
            seeded.ReceptionEmail = reception.Email;
        });
        return seeded;
    }

    public static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new LoginDto { Email = email, Password = Password }))
            .EnsureSuccessStatusCode();
        return client;
    }

    public sealed class SeededConsultation
    {
        public int ClinicaId { get; set; }
        public int AdminId { get; set; }
        public int ProfessionalId { get; set; }
        public int OtherProfessionalId { get; set; }
        public int PatientId { get; set; }
        public int PlanId { get; set; }
        public int AppointmentId { get; set; }
        public string AdminEmail { get; set; } = string.Empty;
        public string ProfessionalEmail { get; set; } = string.Empty;
        public string OtherProfessionalEmail { get; set; } = string.Empty;
        public string ReceptionEmail { get; set; } = string.Empty;
    }
}
