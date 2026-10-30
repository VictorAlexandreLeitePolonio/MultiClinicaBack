using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using MultiClinica.API.Services.MercadoPago;

namespace MultiClinica.Tests;

// Cenário comum dos testes de pagamento online: clínica com conta Mercado Pago
// conectada, paciente e uma cobrança. Nenhum teste passa pelo OAuth (Trilha A).
internal static class MercadoPagoTestSupport
{
    public const string Password = "senha-super-forte";
    public const string WebhookSecret = "mp-webhook-secret";
    public const long DefaultMpUserId = 777;
    public const string PublicKey = "TEST-public-key";

    public static MultiClinicaFactory CreateApp(
        FakeMercadoPagoClient fake, Action<IServiceCollection>? configure = null) => new()
    {
        ConfigureTestServices = services =>
        {
            services.RemoveAll<IMercadoPagoClient>();
            services.AddSingleton<IMercadoPagoClient>(fake);
            services.RemoveAll<IConfigureOptions<MercadoPagoOptions>>();
            services.Configure<MercadoPagoOptions>(options =>
            {
                options.TokenEncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                options.WebhookSecret = WebhookSecret;
                options.RedirectUri = "https://api.test.local/api/mercadopago/callback";
            });
            configure?.Invoke(services);
        },
    };

    public static async Task<int> SeedPatientAccountAsync(
        MultiClinicaFactory app, string email, string name = "Ana Paciente")
    {
        var id = 0;
        await app.SeedAsync(async db =>
        {
            var account = new PatientAccount
            {
                Name = name,
                Email = email,
                Status = PatientAccountStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                ActivatedAt = DateTime.UtcNow,
            };
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            id = account.Id;
        });
        return id;
    }

    public static async Task<SeededClinic> SeedClinicAsync(
        MultiClinicaFactory app, ClinicSeedOptions options)
    {
        var cipher = app.Services.GetRequiredService<TokenCipher>();
        var seeded = new SeededClinic();

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = options.ClinicName,
                NomeFantasia = options.ClinicName,
                NomeResponsavel = "Admin",
                Email = $"contato-{options.AdminEmail}",
                Plan = options.Plan,
                OnlineAcceptPix = options.AcceptPix,
                OnlineAcceptCredit = options.AcceptCredit,
                OnlineMaxInstallments = options.MaxInstallments,
                OnlineAcceptDebit = options.AcceptDebit,
                TimeZoneId = "America/Sao_Paulo",
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();
            seeded.ClinicaId = clinic.Id;

            var admin = new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = options.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Administrador,
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            seeded.AdminId = admin.Id;

            var patient = new Patient
            {
                ClinicaId = clinic.Id,
                Name = options.PatientName,
                Email = options.PatientEmail,
                PatientAccountId = options.PatientAccountId,
                IsActive = true,
            };
            db.Patients.Add(patient);

            var sessionType = new SessionType { ClinicaId = clinic.Id, Name = "Fisioterapia" };
            db.SessionTypes.Add(sessionType);
            await db.SaveChangesAsync();
            seeded.PatientId = patient.Id;

            var plan = new Plans
            {
                ClinicaId = clinic.Id,
                Name = options.PlanName,
                Valor = options.Amount,
                TipoPlano = TipoPlano.Mensal,
                TipoSessaoId = sessionType.Id,
            };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
            seeded.PlanId = plan.Id;

            if (options.ConnectMp)
            {
                db.ClinicMercadoPagoAccounts.Add(new ClinicMercadoPagoAccount
                {
                    ClinicaId = clinic.Id,
                    MpUserId = options.MpUserId,
                    PublicKey = PublicKey,
                    AccessTokenEncrypted = cipher.Encrypt("tok"),
                    RefreshTokenEncrypted = cipher.Encrypt("refresh"),
                    ExpiresAt = DateTime.UtcNow.AddDays(60),
                    LiveMode = false,
                    Status = MercadoPagoAccountStatus.Connected,
                    ConnectedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            var payment = new Payment
            {
                ClinicaId = clinic.Id,
                UserId = admin.Id,
                PatientId = patient.Id,
                PlanId = plan.Id,
                ReferenceMonth = options.ReferenceMonth,
                Amount = options.Amount,
                PaymentMethod = options.PaymentMethod,
                Status = options.Status,
                PaidAt = options.PaidAt,
                MpPaymentId = options.MpPaymentId,
                MpStatus = options.MpStatus,
                MpStatusDetail = options.MpStatusDetail,
                PaidOnline = options.PaidOnline,
                RefundedAt = options.RefundedAt,
                OnlineAlert = options.OnlineAlert,
                PaymentLinkToken = options.PaymentLinkToken,
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            seeded.PaymentId = payment.Id;
        });

        return seeded;
    }

    public static async Task<int> SeedPlanAsync(
        MultiClinicaFactory app, int clinicaId, string name, decimal amount)
    {
        var id = 0;
        await app.SeedAsync(async db =>
        {
            var sessionType = await db.SessionTypes.FirstAsync(item => item.ClinicaId == clinicaId);
            var plan = new Plans
            {
                ClinicaId = clinicaId,
                Name = name,
                Valor = amount,
                TipoPlano = TipoPlano.Mensal,
                TipoSessaoId = sessionType.Id,
            };
            db.Plans.Add(plan);
            await db.SaveChangesAsync();
            id = plan.Id;
        });
        return id;
    }

    public static async Task<HttpClient> LoginAdminAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = Password,
        })).EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<HttpClient> LoginPatientAsync(MultiClinicaFactory app, string email)
    {
        var client = app.CreateClient();
        (await client.PostAsJsonAsync("/api/patient-auth/login", new { email, password = Password }))
            .EnsureSuccessStatusCode();
        return client;
    }

    public sealed class SeededClinic
    {
        public int ClinicaId { get; set; }
        public int AdminId { get; set; }
        public int PatientId { get; set; }
        public int PlanId { get; set; }
        public int PaymentId { get; set; }
    }
}

internal sealed record ClinicSeedOptions
{
    public string AdminEmail { get; init; } = "mp-admin@test.local";
    public string ClinicName { get; init; } = "Clínica MP";
    public ClinicPlan Plan { get; init; } = ClinicPlan.Pro;
    public bool ConnectMp { get; init; } = true;
    public long MpUserId { get; init; } = MercadoPagoTestSupport.DefaultMpUserId;
    public bool AcceptPix { get; init; } = true;
    public bool AcceptCredit { get; init; } = true;
    public int MaxInstallments { get; init; } = 1;
    public bool AcceptDebit { get; init; } = true;
    public string? PatientName { get; init; } = "Ana Paciente";
    public string? PatientEmail { get; init; } = "paciente@test.local";
    public int? PatientAccountId { get; init; }
    public PaymentStatus Status { get; init; } = PaymentStatus.Pending;
    public decimal Amount { get; init; } = 150m;
    public DateOnly ReferenceMonth { get; init; } = new(2026, 9, 1);
    public string PlanName { get; init; } = "Mensal";
    public string PaymentMethod { get; init; } = "Pix";
    public long? MpPaymentId { get; init; }
    public string? MpStatus { get; init; }
    public string? MpStatusDetail { get; init; }
    public bool PaidOnline { get; init; }
    public DateOnly? PaidAt { get; init; }
    public DateTime? RefundedAt { get; init; }
    public string? OnlineAlert { get; init; }
    public string? PaymentLinkToken { get; init; }
}
