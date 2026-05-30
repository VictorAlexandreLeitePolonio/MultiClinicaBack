using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;
using MultiClinica.API.Services;
using Xunit;

namespace MultiClinica.Tests;

public class AuthIsolationTests
{
    [Fact]
    public async Task Login_returns_generic_error_for_invalid_credentials()
    {
        await using var app = new MultiClinicaFactory();
        using var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "missing@example.com",
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Email ou senha inválidos", body);
    }

    [Fact]
    public async Task Login_blocks_inactive_clinic_with_explicit_error()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinica = new Clinica { Nome = "Clinica Inativa", NomeResponsavel = "Victor", IsActive = false };
            db.Clinicas.Add(clinica);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinica.Id,
                Name = "Administrador",
                Email = "admin@inactive.test",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador,
                IsActive = true
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = "admin@inactive.test",
            Password = "secret123"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Clínica inativa", body);
    }

    [Fact]
    public async Task Clinic_user_gets_404_for_patient_from_another_clinic()
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
            db.Patients.Add(new Patient
            {
                ClinicaId = clinicB.Id,
                Name = "Paciente B"
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.GetAsync("/api/patients/1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_cannot_create_superadmin()
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
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            name = "Root",
            email = "root@test.local",
            password = "secret123",
            role = "SuperAdmin"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_creates_clinic_with_first_administrator()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(SeedSuperAdminAsync);

        using var client = app.CreateClient();
        await LoginAsync(client, "root@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/superadmin/clinicas", new
        {
            nome = "Clinica Nova LTDA",
            nomeFantasia = "Clinica Nova",
            nomeResponsavel = "Victor",
            cnpj = "12.345.678/0001-90",
            email = "contato@clinica.test",
            telefone = "(11) 99999-9999",
            cep = "12345-678",
            firstAdmin = new
            {
                name = "Admin Clinica",
                email = "admin@clinica.test",
                password = "secret123"
            }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(c => c.Email == "contato@clinica.test");
            Assert.True(clinic.IsActive);
            Assert.False(clinic.CobrancaAtiva);
            Assert.Equal("12345678000190", clinic.Cnpj);
            Assert.True(await db.Users.AnyAsync(u =>
                u.ClinicaId == clinic.Id
                && u.Email == "admin@clinica.test"
                && u.Role == UserRole.Administrador));
            Assert.True(await db.CommercialHistoryEvents.AnyAsync(h =>
                h.ClinicaId == clinic.Id && h.Type == CommercialHistoryEventType.ClinicCreated));
        });
    }

    [Fact]
    public async Task Manual_unblock_requires_reason()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            await SeedSuperAdminAsync(db);
            db.Clinicas.Add(new Clinica
            {
                Nome = "Bloqueada",
                NomeResponsavel = "Victor",
                IsBlockedByBilling = true
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "root@test.local", "secret123");

        var response = await client.PostAsJsonAsync("/api/superadmin/clinicas/2/billing/unblock", new
        {
            reason = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Attachment_upload_returns_404_when_medical_record_is_from_other_clinic()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            var clinicA = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            var clinicB = new Clinica { Nome = "Clinica B", NomeResponsavel = "Victor" };
            db.Clinicas.AddRange(clinicA, clinicB);
            await db.SaveChangesAsync();

            var user = new User
            {
                ClinicaId = clinicA.Id,
                Name = "Admin A",
                Email = "admin-a@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
                Role = UserRole.Administrador
            };
            var patientA = new Patient { ClinicaId = clinicA.Id, Name = "Paciente A" };
            var patientB = new Patient { ClinicaId = clinicB.Id, Name = "Paciente B" };
            db.Users.Add(user);
            db.Patients.AddRange(patientA, patientB);
            await db.SaveChangesAsync();

            db.MedicalRecords.Add(new MedicalRecord
            {
                ClinicaId = clinicB.Id,
                PatientId = patientB.Id,
                UserId = user.Id,
                Titulo = "Outro tenant"
            });
            await db.SaveChangesAsync();
        });

        using var client = app.CreateClient();
        await LoginAsync(client, "admin-a@test.local", "secret123");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("1"), "patientId");
        form.Add(new StringContent("1"), "medicalRecordId");
        form.Add(new StringContent("Exam"), "type");
        form.Add(new ByteArrayContent("arquivo"u8.ToArray()), "file", "exame.png");

        var response = await client.PostAsync("/api/attachments", form);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Billing_service_generates_monthly_charge_and_blocks_overdue_clinic()
    {
        await using var app = new MultiClinicaFactory();
        await app.SeedAsync(async db =>
        {
            db.Clinicas.Add(new Clinica
            {
                Nome = "Clinica Cobrança",
                NomeResponsavel = "Victor",
                CobrancaAtiva = true,
                ValorMensalidade = 300,
                DiaVencimento = 10,
                DataInicioCobranca = new DateOnly(2026, 1, 1)
            });
            await db.SaveChangesAsync();
        });

        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IClinicaBillingService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await service.GenerateMonthlyChargesAsync(new DateOnly(2026, 5, 1));
        var charge = await dbContext.ClinicCharges.SingleAsync();

        Assert.Equal("2026-05", charge.ReferenceMonth);
        Assert.Equal(new DateOnly(2026, 5, 10), charge.DueDate);
        Assert.Equal(ClinicChargeStatus.Pending, charge.Status);

        await service.BlockOverdueClinicsAsync(new DateOnly(2026, 5, 11));
        var clinic = await dbContext.Clinicas.SingleAsync();

        Assert.True(clinic.IsBlockedByBilling);
        Assert.True(await dbContext.CommercialHistoryEvents.AnyAsync(h =>
            h.Type == CommercialHistoryEventType.AutomaticBillingBlock));
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

    private static async Task SeedSuperAdminAsync(AppDbContext db)
    {
        var clinic = new Clinica
        {
            Nome = "Admin Interno",
            NomeResponsavel = "Victor"
        };
        db.Clinicas.Add(clinic);
        await db.SaveChangesAsync();

        db.Users.Add(new User
        {
            ClinicaId = clinic.Id,
            Name = "Victor",
            Email = "root@test.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
            Role = UserRole.SuperAdmin
        });
        await db.SaveChangesAsync();
    }
}

public sealed class MultiClinicaFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            var storageDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IAttachmentStorage));
            if (storageDescriptor is not null)
                services.Remove(storageDescriptor);
            services.AddSingleton<IAttachmentStorage, FakeAttachmentStorage>();
        });
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await seed(db);
    }
}

public class FakeAttachmentStorage : IAttachmentStorage
{
    public Task<string> SaveAsync(Stream stream, string objectKey, string contentType, CancellationToken cancellationToken = default)
        => Task.FromResult(objectKey);
}
