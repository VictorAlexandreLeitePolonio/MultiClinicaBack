using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Auth;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Interfaces;
using Xunit;

namespace MultiClinica.Tests;

public class PatientImportTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record ImportResponse(
        Guid ImportId,
        string Status,
        int TotalRows,
        int ImportedCount,
        int RejectedCount,
        int EmailsQueuedCount,
        int EmailsSkippedNoEmailCount,
        IReadOnlyList<ImportRowResult> Results);

    private sealed record ImportRowResult(
        int Row,
        string Status,
        int? PatientId,
        string? EmailStatus,
        IReadOnlyList<ImportRowError>? Errors);

    private sealed record ImportRowError(string Field, string Code, string Message);

    [Fact]
    public async Task Import_csv_name_only_persists_valid_rows_in_authenticated_clinic_and_reports_invalid_rows()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(async db =>
        {
            await SeedClinicAsync(db, "Clinica A", "admin-a@test.local");
            await SeedClinicAsync(db, "Clinica B", "admin-b@test.local");
        });
        using var client = await LoginAsync(app, "admin-a@test.local");
        var startedAt = DateTime.UtcNow;

        using var response = await ImportCsvAsync(client,
            "Name;Email;CPF;Phone;Rua;Numero\n Ana da Silva ;;;;;\n;bad-row@example.com;;;;",
            "patients.csv", Guid.NewGuid().ToString());
        var completedAt = DateTime.UtcNow;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.NotNull(result);
        Assert.Equal("CompletedWithErrors", result!.Status);
        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.RejectedCount);
        Assert.Equal(1, result.EmailsSkippedNoEmailCount);
        Assert.Equal(2, result.Results[0].Row);
        Assert.Equal("SkippedNoEmail", result.Results[0].EmailStatus);
        Assert.Equal(3, result.Results[1].Row);
        Assert.Contains(result.Results[1].Errors!, error => error.Field == "Name" && error.Code == "EMPTY_FIELD");

        await app.SeedAsync(async db =>
        {
            var patient = await db.Patients.SingleAsync();
            var clinicA = await db.Clinicas.SingleAsync(clinic => clinic.Nome == "Clinica A");
            var userA = await db.Users.SingleAsync(user => user.Email == "admin-a@test.local");
            Assert.Equal("Ana da Silva", patient.Name);
            Assert.Equal(clinicA.Id, patient.ClinicaId);
            Assert.Equal(userA.Id, patient.CreatedByUserId);
            Assert.Null(patient.PatientAccountId);
            Assert.Null(patient.Email);
            Assert.Null(patient.CPF);
            Assert.Null(patient.Phone);
            Assert.True(patient.IsActive);
            Assert.False(patient.IsDeleted);
            Assert.Equal(patient.CreatedAt, patient.UpdatedAt);
            Assert.InRange(DateTime.SpecifyKind(patient.CreatedAt, DateTimeKind.Utc), startedAt, completedAt);
        });
    }

    [Fact]
    public async Task Import_same_idempotency_key_returns_original_result_without_duplicating_patients()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");
        const string csv = "Name\nMaria";
        var key = Guid.NewGuid().ToString();

        using var first = await ImportCsvAsync(client, csv, "patients.csv", key);
        using var retry = await ImportCsvAsync(client, csv, "patients.csv", key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var firstResult = await first.Content.ReadFromJsonAsync<ImportResponse>(Json);
        var retryResult = await retry.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.Equal(firstResult!.ImportId, retryResult!.ImportId);
        await app.SeedAsync(async db => Assert.Equal(1, await db.Patients.CountAsync()));

        using var conflict = await ImportCsvAsync(client, "Name\nJoana", "patients.csv", key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Import_server_managed_headers_rejects_file_before_persisting_any_patient()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "Name,ClinicaId,CreatedByUserId\nMaria,999,999",
            "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await app.SeedAsync(async db => Assert.Empty(await db.Patients.ToListAsync()));
    }

    [Fact]
    public async Task Import_csv_with_bom_quoted_delimiter_and_multiline_field_preserves_name()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "\uFEFF email ; NAME\r\n maria@example.com ;\"Maria, da Silva\r\nFilha\"",
            "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.NotNull(result);
        Assert.Equal(1, result!.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        await app.SeedAsync(async db =>
        {
            var patient = await db.Patients.SingleAsync();
            Assert.Equal("Maria, da Silva\r\nFilha", patient.Name);
            Assert.Equal("maria@example.com", patient.Email);
        });
    }

    [Fact]
    public async Task Import_csv_ignores_blank_lines_and_preserves_original_row_number()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "\nName\n\nMaria\n", "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.NotNull(result);
        Assert.Equal(1, result!.TotalRows);
        Assert.Equal(4, Assert.Single(result.Results).Row);
    }

    [Fact]
    public async Task Import_rejects_files_over_the_configured_row_limit_before_persisting()
    {
        await using var app = new PatientImportFactory(maxRows: 1);
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "Name\nMaria\nAna", "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        await app.SeedAsync(async db => Assert.Empty(await db.Patients.ToListAsync()));
    }

    [Fact]
    public async Task Import_csv_with_inconsistent_row_width_rejects_file_without_partial_persistence()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "Name,Email\nMaria,maria@example.com\nJoana,joana@example.com,extra",
            "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await app.SeedAsync(async db => Assert.Empty(await db.Patients.ToListAsync()));
    }

    [Fact]
    public async Task Import_csv_with_duplicate_headers_after_case_normalization_is_rejected()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "Name, name \nMaria,Joana",
            "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await app.SeedAsync(async db => Assert.Empty(await db.Patients.ToListAsync()));
    }

    [Fact]
    public async Task Import_xlsx_with_pending_portal_account_accepts_nullable_cpf_and_phone_and_queues_email()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportXlsxAsync(client,
            ["Name", "Email", "CPF", "Phone"],
            ["João da Silva", "joao@example.com", "", ""],
            Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.NotNull(result);
        Assert.Equal(1, result!.ImportedCount);
        Assert.Equal(1, result.EmailsQueuedCount);
        Assert.Equal(0, result.EmailsSkippedNoEmailCount);
        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync();
            Assert.Equal("joao@example.com", account.Email);
            Assert.Null(account.CPF);
            Assert.Null(account.Phone);
            Assert.Equal(PatientAccountStatus.PendingActivation, account.Status);
            Assert.Null(account.PasswordHash);
        });
    }

    [Fact]
    public async Task Import_reuses_existing_account_without_overwriting_its_identity_data()
    {
        await using var app = new PatientImportFactory();
        var accountId = 0;
        const string passwordHash = "existing-password-hash";
        await app.SeedAsync(async db =>
        {
            await SeedClinicAsync(db, "Clinica A", "admin-a@test.local");
            var account = new PatientAccount
            {
                Name = "Nome já existente",
                Email = "existing@example.com",
                CPF = "12345678901",
                Phone = "11999998888",
                PasswordHash = passwordHash,
                Status = PatientAccountStatus.Active
            };
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
        });
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "Name;Email;CPF;Phone\nNome importado;existing@example.com;123.456.789-01;21911112222",
            "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.Equal(1, result!.ImportedCount);
        Assert.Equal(1, result.EmailsQueuedCount);
        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync(item => item.Id == accountId);
            Assert.Equal("Nome já existente", account.Name);
            Assert.Equal("existing@example.com", account.Email);
            Assert.Equal("12345678901", account.CPF);
            Assert.Equal("11999998888", account.Phone);
            Assert.Equal(passwordHash, account.PasswordHash);
            Assert.Equal(PatientAccountStatus.Active, account.Status);

            var patient = await db.Patients.SingleAsync();
            Assert.Equal("Nome importado", patient.Name);
            Assert.Equal("21911112222", patient.Phone);
            Assert.Equal(accountId, patient.PatientAccountId);
            var job = await db.PatientEmailOutbox.SingleAsync();
            Assert.Equal(PatientEmailOutboxType.ClinicLinkNotice, job.Type);
        });
    }

    [Fact]
    public async Task Import_xlsx_treats_omitted_trailing_optional_cells_as_blank()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportXlsxAsync(client,
            ["Name", "Email"], ["Maria"], Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.NotNull(result);
        Assert.Equal(1, result!.ImportedCount);
        Assert.Equal(1, result.EmailsSkippedNoEmailCount);
        await app.SeedAsync(async db =>
        {
            var patient = await db.Patients.SingleAsync();
            Assert.Equal("Maria", patient.Name);
            Assert.Null(patient.Email);
        });
    }

    [Fact]
    public async Task Import_xlsx_rejects_archive_with_excessive_uncompressed_size()
    {
        await using var app = new PatientImportFactory();
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportXlsxAsync(client,
            ["Name"], ["Maria"], Guid.NewGuid().ToString(), inflatedEntryCharacters: 50_000_001);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await app.SeedAsync(async db => Assert.Empty(await db.Patients.ToListAsync()));
    }

    [Fact]
    public async Task Activate_second_valid_token_after_account_activation_is_rejected_without_overwriting_password()
    {
        await using var app = new PatientImportFactory();
        var accountId = 0;
        await app.SeedAsync(async db =>
        {
            var account = new PatientAccount
            {
                Email = "pending@example.com",
                Status = PatientAccountStatus.PendingActivation
            };
            db.PatientAccounts.Add(account);
            await db.SaveChangesAsync();
            accountId = account.Id;
            await SeedActivationTokenAsync(db, accountId, "first-activation-token");
            await SeedActivationTokenAsync(db, accountId, "second-activation-token");
        });
        using var client = app.CreateClient();

        using var first = await client.PostAsJsonAsync("/api/patient-auth/activate",
            new { token = "first-activation-token", password = "password123" });
        using var second = await client.PostAsJsonAsync("/api/patient-auth/activate",
            new { token = "second-activation-token", password = "overwritten123" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        await app.SeedAsync(async db =>
        {
            var account = await db.PatientAccounts.SingleAsync(item => item.Id == accountId);
            Assert.True(BCrypt.Net.BCrypt.Verify("password123", account.PasswordHash));
            Assert.False(BCrypt.Net.BCrypt.Verify("overwritten123", account.PasswordHash));
            Assert.All(await db.PatientAuthTokens.ToListAsync(), token => Assert.NotNull(token.ConsumedAt));
        });
    }

    [Fact]
    public async Task Outbox_worker_sends_pending_activation_invite_after_import_commits()
    {
        var emailSender = new PatientImportEmailSender();
        await using var app = new PatientImportFactory(enableOutboxWorker: true, emailSender);
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "Name,Email\n<img src=x onerror=alert(1)>,maria@example.com",
            "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Json);
        Assert.Equal(1, result!.EmailsQueuedCount);
        await WaitForOutboxAsync(app, PatientEmailOutboxStatus.Completed);
        Assert.Single(emailSender.Messages);
        Assert.Equal("Ative seu acesso ao portal do paciente", emailSender.Messages.Single().Subject);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", emailSender.Messages.Single().Body);
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", emailSender.Messages.Single().Body);
        await app.SeedAsync(async db =>
        {
            var job = await db.PatientEmailOutbox.SingleAsync();
            Assert.Equal(1, job.AttemptCount);
            Assert.Null(job.LastError);
            Assert.Single(await db.PatientAuthTokens.Where(token => token.Type == PatientAuthTokenType.Activation).ToListAsync());
        });
    }

    [Fact]
    public async Task Outbox_worker_uses_current_account_status_instead_of_stale_activation_type()
    {
        var emailSender = new PatientImportEmailSender();
        await using var app = new PatientImportFactory(enableOutboxWorker: true, emailSender);

        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica { Nome = "Clinica A", NomeResponsavel = "Victor" };
            var account = new PatientAccount
            {
                Name = "Maria",
                Email = "maria@example.com",
                Status = PatientAccountStatus.Active,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
            };
            var patient = new Patient { Clinica = clinic, PatientAccount = account, Name = "Maria", Email = account.Email };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            db.PatientEmailOutbox.Add(new PatientEmailOutbox
            {
                ClinicaId = clinic.Id,
                PatientId = patient.Id,
                PatientAccountId = account.Id,
                Type = PatientEmailOutboxType.ActivationInvitation,
                DeduplicationKey = "stale-activation-test",
                NextAttemptAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });

        await WaitForOutboxAsync(app, PatientEmailOutboxStatus.Completed);
        Assert.Single(emailSender.Messages);
        Assert.Equal("Você foi vinculado a uma nova clínica", emailSender.Messages.Single().Subject);
        await app.SeedAsync(async db =>
        {
            var job = await db.PatientEmailOutbox.SingleAsync();
            Assert.Equal(PatientEmailOutboxType.ClinicLinkNotice, job.Type);
            Assert.Empty(await db.PatientAuthTokens.ToListAsync());
        });
    }

    [Fact]
    public async Task Outbox_worker_retries_failed_email_without_reverting_imported_patient()
    {
        var emailSender = new PatientImportEmailSender { Fail = true };
        await using var app = new PatientImportFactory(enableOutboxWorker: true, emailSender);
        await app.SeedAsync(db => SeedClinicAsync(db, "Clinica A", "admin-a@test.local"));
        using var client = await LoginAsync(app, "admin-a@test.local");

        using var response = await ImportCsvAsync(client,
            "Name,Email\nJoana,joana@example.com",
            "patients.csv", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WaitForOutboxAsync(app, PatientEmailOutboxStatus.Failed, minimumAttempts: 2);
        await app.SeedAsync(async db =>
        {
            Assert.Equal(1, await db.Patients.CountAsync());
            var job = await db.PatientEmailOutbox.SingleAsync();
            Assert.Equal(2, job.AttemptCount);
            Assert.Null(job.LeaseToken);
            Assert.Equal("Falha no envio da notificação.", job.LastError);
        });
    }

    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app, string email)
    {
        var client = app.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginDto
        {
            Email = email,
            Password = "secret123"
        });
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<HttpResponseMessage> ImportCsvAsync(HttpClient client, string csv, string fileName, string key)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/patients/import") { Content = form };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ImportXlsxAsync(
        HttpClient client,
        string[] headers,
        string[] values,
        string key,
        int inflatedEntryCharacters = 0)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(CreateXlsx(headers, values, inflatedEntryCharacters));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "patients.xlsx");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/patients/import") { Content = form };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static byte[] CreateXlsx(string[] headers, string[] values, int inflatedEntryCharacters = 0)
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            AddEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            AddEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Patients" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            AddEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            AddEntry(archive, "xl/worksheets/sheet1.xml", BuildSheet(headers, values));
            if (inflatedEntryCharacters > 0)
                AddEntry(archive, "xl/media/unused.bin", new string('x', inflatedEntryCharacters));
        }
        return stream.ToArray();
    }

    private static string BuildSheet(string[] headers, string[] values)
    {
        static string Cell(string column, int row, string value)
            => $"<c r=\"{column}{row}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(value)}</t></is></c>";
        var row1 = string.Join("", headers.Select((value, index) => Cell(((char)('A' + index)).ToString(), 1, value)));
        var row2 = string.Join("", values.Select((value, index) => Cell(((char)('A' + index)).ToString(), 2, value)));
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData><row r="1">{row1}</row><row r="2">{row2}</row></sheetData></worksheet>
            """;
    }

    private static void AddEntry(System.IO.Compression.ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static async Task SeedClinicAsync(AppDbContext db, string name, string adminEmail)
    {
        var clinic = new Clinica { Nome = name, NomeResponsavel = "Victor" };
        db.Clinicas.Add(clinic);
        await db.SaveChangesAsync();
        db.Users.Add(new User
        {
            ClinicaId = clinic.Id,
            Name = "Admin " + name,
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("secret123"),
            Role = UserRole.Administrador
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedActivationTokenAsync(AppDbContext db, int accountId, string rawToken)
    {
        db.PatientAuthTokens.Add(new PatientAuthToken
        {
            PatientAccountId = accountId,
            Type = PatientAuthTokenType.Activation,
            TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();
    }

    private static async Task WaitForOutboxAsync(
        PatientImportFactory app,
        PatientEmailOutboxStatus expectedStatus,
        int minimumAttempts = 0)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            var complete = false;
            await app.SeedAsync(async db =>
            {
                var job = await db.PatientEmailOutbox.FirstOrDefaultAsync();
                complete = job?.Status == expectedStatus && job.AttemptCount >= minimumAttempts;
            });
            if (complete)
                return;
            await Task.Delay(100);
        }
        var diagnostic = "nenhum job";
        await app.SeedAsync(async db =>
        {
            var job = await db.PatientEmailOutbox.FirstOrDefaultAsync();
            if (job is not null)
                diagnostic = $"status={job.Status}; attempts={job.AttemptCount}; next={job.NextAttemptAt:O}; lease={job.LeaseToken}; error={job.LastError}";
        });
        var hostedServices = string.Join(", ", app.Services.GetServices<IHostedService>().Select(service => service.GetType().Name));
        var configuration = app.Services.GetRequiredService<IConfiguration>();
        Assert.Fail($"A fila não chegou ao estado {expectedStatus}. Job: {diagnostic}. Enabled={configuration["PatientEmailOutbox:Enabled"]}; Environment={app.Services.GetRequiredService<IWebHostEnvironment>().EnvironmentName}; Hosted services: {hostedServices}.");
    }
}

public sealed class PatientImportFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"patient-import-{Guid.NewGuid():N}";
    private readonly SqliteConnection _connection;
    private readonly bool _enableOutboxWorker;
    private readonly IEmailSender? _emailSender;
    private readonly int _maxRows;

    public PatientImportFactory(bool enableOutboxWorker = false, IEmailSender? emailSender = null, int maxRows = 10_000)
    {
        _enableOutboxWorker = enableOutboxWorker;
        _emailSender = emailSender;
        _maxRows = maxRows;
        _connection = new SqliteConnection($"Data Source={_databaseName};Mode=Memory;Cache=Shared");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["PatientEmailOutbox:Enabled"] = _enableOutboxWorker.ToString(),
                ["PatientEmailOutbox:PollSeconds"] = "1",
                ["PatientEmailOutbox:RetryBaseSeconds"] = "1",
                ["PatientEmailOutbox:MaxAttempts"] = "2",
                ["PatientImport:MaxRows"] = _maxRows.ToString()
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(
                $"Data Source={_databaseName};Mode=Memory;Cache=Shared"));
            if (_enableOutboxWorker)
                services.AddHostedService<PatientEmailOutboxWorker>();
            if (_emailSender is not null)
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(_emailSender);
            }
        });
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await seed(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}

public sealed class PatientImportEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<(string To, string Subject, string Body)> _messages = new();

    public bool Fail { get; set; }
    public IReadOnlyCollection<(string To, string Subject, string Body)> Messages => _messages.ToArray();

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (Fail)
            throw new InvalidOperationException("Provider indisponível.");
        _messages.Enqueue((to, subject, htmlBody));
        return Task.CompletedTask;
    }
}
