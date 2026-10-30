using System.Buffers.Text;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.MercadoPago;
using Xunit;

namespace MultiClinica.Tests;

public class MercadoPagoOAuthTests
{
    private const string Password = "senha-super-forte";
    private const string AdminEmail = "mp-admin@test.local";
    private const string FreeEmail = "mp-free@test.local";
    private const string ClientId = "client-1";
    private const string RedirectUri = "https://api.test/api/mercadopago/callback";

    private static readonly byte[] EncryptionKey = RandomNumberGenerator.GetBytes(32);

    private static MultiClinicaFactory CreateApp(
        FakeMercadoPagoClient fake, string? encryptionKey = null, string? clientId = ClientId) => new()
    {
        ConfigureTestServices = services =>
        {
            services.RemoveAll<IMercadoPagoClient>();
            services.AddSingleton<IMercadoPagoClient>(fake);
            services.RemoveAll<IConfigureOptions<MercadoPagoOptions>>();
            services.Configure<MercadoPagoOptions>(options =>
            {
                options.ClientId = clientId ?? string.Empty;
                options.ClientSecret = "secret-1";
                options.RedirectUri = RedirectUri;
                options.TokenEncryptionKey = encryptionKey ?? Convert.ToBase64String(EncryptionKey);
                options.ApiBaseUrl = "https://api.test/";
                options.AuthBaseUrl = "https://auth.test/";
            });
        },
    };

    // ── Semeadura ────────────────────────────────────────────────────────────

    private static async Task<int> SeedClinicAsync(
        MultiClinicaFactory app, ClinicPlan plan = ClinicPlan.Pro, string email = AdminEmail)
    {
        var id = 0;
        await app.SeedAsync(async db =>
        {
            var clinic = new Clinica
            {
                Nome = "Clínica Teste",
                NomeFantasia = "Clínica Teste",
                NomeResponsavel = "Admin",
                Email = $"clinica-{email}",
                Telefone = "11999999999",
                Plan = plan,
                TimeZoneId = "America/Sao_Paulo",
            };
            db.Clinicas.Add(clinic);
            await db.SaveChangesAsync();

            db.Users.Add(new User
            {
                ClinicaId = clinic.Id,
                Name = "Admin",
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                Role = UserRole.Administrador,
            });
            await db.SaveChangesAsync();
            id = clinic.Id;
        });
        return id;
    }

    private static async Task<int> SeedAccountAsync(
        MultiClinicaFactory app,
        TokenCipher cipher,
        long mpUserId,
        DateTime expiresAt,
        MercadoPagoAccountStatus status = MercadoPagoAccountStatus.Connected,
        string accessToken = "access-old",
        string refreshToken = "refresh-old",
        int? clinicaId = null)
    {
        var id = 0;
        await app.SeedAsync(async db =>
        {
            var targetClinicId = clinicaId ?? 0;
            if (targetClinicId == 0)
            {
                var clinic = new Clinica
                {
                    Nome = $"Clínica {mpUserId}",
                    NomeFantasia = $"Clínica {mpUserId}",
                    NomeResponsavel = "Admin",
                    Email = $"clinica-{mpUserId}@test.local",
                };
                db.Clinicas.Add(clinic);
                await db.SaveChangesAsync();
                targetClinicId = clinic.Id;
            }

            var account = new ClinicMercadoPagoAccount
            {
                ClinicaId = targetClinicId,
                MpUserId = mpUserId,
                PublicKey = "PUB-1",
                AccessTokenEncrypted = cipher.Encrypt(accessToken),
                RefreshTokenEncrypted = cipher.Encrypt(refreshToken),
                ExpiresAt = expiresAt,
                LiveMode = true,
                Status = status,
                ConnectedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
            };
            db.ClinicMercadoPagoAccounts.Add(account);
            await db.SaveChangesAsync();
            id = account.Id;
        });
        return id;
    }

    private static async Task<HttpClient> LoginAsync(MultiClinicaFactory app, string email = AdminEmail)
    {
        var client = app.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static HttpClient NoRedirectClient(MultiClinicaFactory app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> ConnectAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/mercadopago/connect", null);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("url").GetString()!;
    }

    private static Dictionary<string, string> Query(string url) =>
        new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static Task<HttpResponseMessage> CallbackAsync(HttpClient client, string code, string state) =>
        client.GetAsync($"/api/mercadopago/callback?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}");

    private static void AssertRedirectsTo(HttpResponseMessage response, string suffix)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith($"/app/configuracoes?mp={suffix}", response.Headers.Location!.ToString());
    }

    // ── Connect ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Connect_returns_authorization_url_and_stores_hashed_state()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient());
        var clinicId = await SeedClinicAsync(app);
        using var client = await LoginAsync(app);

        var url = await ConnectAsync(client);

        Assert.StartsWith("https://auth.test/authorization?", url);
        var query = Query(url);
        Assert.Equal(ClientId, query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("mp", query["platform_id"]);
        Assert.Equal(RedirectUri, query["redirect_uri"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        Assert.False(string.IsNullOrWhiteSpace(query["code_challenge"]));

        await app.SeedAsync(async db =>
        {
            var state = await db.MercadoPagoOAuthStates.SingleAsync();
            Assert.Equal(Hash(query["state"]), state.StateHash);
            Assert.Equal(clinicId, state.ClinicaId);
            Assert.True(state.UserId > 0);
            Assert.Equal(Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(state.CodeVerifier))),
                query["code_challenge"]);
            Assert.True(state.ExpiresAt > DateTime.UtcNow.AddMinutes(9));
            Assert.True(state.ExpiresAt <= DateTime.UtcNow.AddMinutes(10));
        });
    }

    [Fact]
    public async Task Connect_requires_paid_plan()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient());
        await SeedClinicAsync(app, ClinicPlan.Free, FreeEmail);
        using var client = await LoginAsync(app, FreeEmail);

        var response = await client.PostAsync("/api/mercadopago/connect", null);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
    }

    [Fact]
    public async Task Connect_without_encryption_key_returns_503()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient(), encryptionKey: string.Empty);
        await SeedClinicAsync(app);
        using var client = await LoginAsync(app);

        var response = await client.PostAsync("/api/mercadopago/connect", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("MERCADOPAGO_NOT_CONFIGURED", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Connect_without_client_id_returns_503()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient(), clientId: string.Empty);
        await SeedClinicAsync(app);
        using var client = await LoginAsync(app);

        var response = await client.PostAsync("/api/mercadopago/connect", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // ── Callback ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Callback_with_unknown_state_redirects_to_error()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient());
        using var client = NoRedirectClient(app);

        var response = await CallbackAsync(client, "code", "desconhecido");

        AssertRedirectsTo(response, "erro");
    }

    [Fact]
    public async Task Callback_with_expired_state_redirects_to_error_and_consumes_it()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient());
        var clinicId = await SeedClinicAsync(app);
        await app.SeedAsync(async db =>
        {
            db.MercadoPagoOAuthStates.Add(new MercadoPagoOAuthState
            {
                StateHash = Hash("expirado"),
                ClinicaId = clinicId,
                UserId = 1,
                CodeVerifier = "verifier",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            });
            await db.SaveChangesAsync();
        });
        using var client = NoRedirectClient(app);

        var response = await CallbackAsync(client, "code", "expirado");

        AssertRedirectsTo(response, "erro");
        await app.SeedAsync(async db =>
            Assert.Empty(await db.MercadoPagoOAuthStates.ToListAsync()));
    }

    [Fact]
    public async Task Callback_success_stores_encrypted_account_and_cannot_be_reused()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = CreateApp(fake);
        var clinicId = await SeedClinicAsync(app);
        fake.NextToken = new MpTokenResponse("access-1", "refresh-1", 15552000, 555, "PUB-1", true);

        using var login = await LoginAsync(app);
        var state = Query(await ConnectAsync(login))["state"];

        using var callback = NoRedirectClient(app);
        var response = await CallbackAsync(callback, "code-1", state);

        AssertRedirectsTo(response, "conectado");
        var exchange = Assert.Single(fake.Exchanges);
        Assert.Equal("code-1", exchange.Code);
        Assert.False(string.IsNullOrWhiteSpace(exchange.Verifier));

        var cipher = app.Services.GetRequiredService<TokenCipher>();
        await app.SeedAsync(async db =>
        {
            var account = await db.ClinicMercadoPagoAccounts.SingleAsync(a => a.ClinicaId == clinicId);
            Assert.Equal(555L, account.MpUserId);
            Assert.Equal("PUB-1", account.PublicKey);
            Assert.True(account.LiveMode);
            Assert.Equal(MercadoPagoAccountStatus.Connected, account.Status);
            Assert.NotEqual("access-1", account.AccessTokenEncrypted);
            Assert.NotEqual("refresh-1", account.RefreshTokenEncrypted);
            Assert.Equal("access-1", cipher.Decrypt(account.AccessTokenEncrypted));
            Assert.Equal("refresh-1", cipher.Decrypt(account.RefreshTokenEncrypted));
            Assert.True(account.ExpiresAt > DateTime.UtcNow.AddDays(150));
            Assert.True(account.ConnectedAt > DateTime.UtcNow.AddMinutes(-1));
        });

        var reused = await CallbackAsync(callback, "code-1", state);
        AssertRedirectsTo(reused, "erro");
        Assert.Single(fake.Exchanges);

        var status = await login.GetFromJsonAsync<JsonElement>("/api/mercadopago/status");
        Assert.True(status.GetProperty("connected").GetBoolean());
        Assert.True(status.GetProperty("liveMode").GetBoolean());
        Assert.False(status.GetProperty("revoked").GetBoolean());
    }

    [Fact]
    public async Task Callback_with_account_from_another_clinic_redirects_to_conta_em_uso()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = CreateApp(fake);
        await SeedClinicAsync(app);
        var cipher = app.Services.GetRequiredService<TokenCipher>();
        await SeedAccountAsync(app, cipher, 555, DateTime.UtcNow.AddDays(60));
        fake.NextToken = new MpTokenResponse("access-1", "refresh-1", 15552000, 555, "PUB-1", false);

        using var login = await LoginAsync(app);
        var state = Query(await ConnectAsync(login))["state"];

        using var callback = NoRedirectClient(app);
        var response = await CallbackAsync(callback, "code-1", state);

        AssertRedirectsTo(response, "conta-em-uso");
        await app.SeedAsync(async db =>
        {
            Assert.Single(await db.ClinicMercadoPagoAccounts.ToListAsync()); // só a da outra clínica
            Assert.Equal(555L, (await db.ClinicMercadoPagoAccounts.SingleAsync()).MpUserId);
        });
    }

    // ── Settings, status e disconnect ────────────────────────────────────────

    [Fact]
    public async Task Settings_reject_no_active_method_and_invalid_installments()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient());
        var clinicId = await SeedClinicAsync(app);
        using var client = await LoginAsync(app);

        var none = await client.PutAsJsonAsync("/api/mercadopago/settings",
            new { acceptPix = false, acceptCredit = false, maxInstallments = 1, acceptDebit = false });
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        using (var document = JsonDocument.Parse(await none.Content.ReadAsStringAsync()))
            Assert.Equal("INVALID_PAYMENT_SETTINGS", document.RootElement.GetProperty("code").GetString());

        var installments = await client.PutAsJsonAsync("/api/mercadopago/settings",
            new { acceptPix = true, acceptCredit = true, maxInstallments = 13, acceptDebit = true });
        Assert.Equal(HttpStatusCode.BadRequest, installments.StatusCode);

        var valid = await client.PutAsJsonAsync("/api/mercadopago/settings",
            new { acceptPix = true, acceptCredit = true, maxInstallments = 6, acceptDebit = false });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        await app.SeedAsync(async db =>
        {
            var clinic = await db.Clinicas.SingleAsync(c => c.Id == clinicId);
            Assert.True(clinic.OnlineAcceptPix);
            Assert.True(clinic.OnlineAcceptCredit);
            Assert.False(clinic.OnlineAcceptDebit);
            Assert.Equal(6, clinic.OnlineMaxInstallments);
        });
    }

    [Fact]
    public async Task Status_defaults_and_disconnect_removes_the_account()
    {
        await using var app = CreateApp(new FakeMercadoPagoClient());
        var clinicId = await SeedClinicAsync(app);
        var cipher = app.Services.GetRequiredService<TokenCipher>();
        var accountId = await SeedAccountAsync(app, cipher, 777, DateTime.UtcNow.AddDays(60), clinicaId: clinicId);
        using var client = await LoginAsync(app);

        var status = await client.GetFromJsonAsync<JsonElement>("/api/mercadopago/status");
        Assert.True(status.GetProperty("connected").GetBoolean());
        Assert.Equal(1, status.GetProperty("maxInstallments").GetInt32());
        Assert.True(status.GetProperty("acceptPix").GetBoolean());

        var response = await client.DeleteAsync("/api/mercadopago");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await app.SeedAsync(async db =>
        {
            Assert.False(await db.ClinicMercadoPagoAccounts.AnyAsync(a => a.Id == accountId));
            Assert.False(await db.ClinicMercadoPagoAccounts.AnyAsync(a => a.ClinicaId == clinicId));
        });
    }

    // ── Renovação ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshDue_renews_only_accounts_expiring_in_less_than_30_days()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = CreateApp(fake);
        var cipher = app.Services.GetRequiredService<TokenCipher>();
        var now = DateTime.UtcNow;

        var due = await SeedAccountAsync(app, cipher, 1, now.AddDays(10));
        var fresh = await SeedAccountAsync(app, cipher, 2, now.AddDays(60));
        var revoked = await SeedAccountAsync(app, cipher, 3, now.AddDays(1),
            MercadoPagoAccountStatus.Revoked);

        fake.NextToken = new MpTokenResponse("access-new", "refresh-new", 15552000, 1, "PUB-1", true);
        var refresher = app.Services.GetRequiredService<MercadoPagoTokenRefresher>();
        await refresher.RefreshDueAsync(CancellationToken.None);

        Assert.Single(fake.Refreshes);
        Assert.Equal("refresh-old", fake.Refreshes[0]);
        await app.SeedAsync(async db =>
        {
            var renewed = await db.ClinicMercadoPagoAccounts.FindAsync(due);
            Assert.NotNull(renewed);
            Assert.Equal("access-new", cipher.Decrypt(renewed!.AccessTokenEncrypted));
            Assert.Equal("refresh-new", cipher.Decrypt(renewed.RefreshTokenEncrypted));
            Assert.True(renewed.ExpiresAt > now.AddDays(150));
            Assert.Equal(MercadoPagoAccountStatus.Connected, renewed.Status);

            var untouched = await db.ClinicMercadoPagoAccounts.FindAsync(fresh);
            Assert.Equal("refresh-old", cipher.Decrypt(untouched!.RefreshTokenEncrypted));

            var skipped = await db.ClinicMercadoPagoAccounts.FindAsync(revoked);
            Assert.Equal(MercadoPagoAccountStatus.Revoked, skipped!.Status);
            Assert.Equal("refresh-old", cipher.Decrypt(skipped.RefreshTokenEncrypted));
        });
    }

    [Fact]
    public async Task RefreshDue_401_revokes_the_account()
    {
        var fake = new FakeMercadoPagoClient();
        await using var app = CreateApp(fake);
        var cipher = app.Services.GetRequiredService<TokenCipher>();
        var accountId = await SeedAccountAsync(app, cipher, 1, DateTime.UtcNow.AddDays(5));

        fake.FailWithStatus = 401;
        var refresher = app.Services.GetRequiredService<MercadoPagoTokenRefresher>();
        await refresher.RefreshDueAsync(CancellationToken.None);

        await app.SeedAsync(async db =>
        {
            var account = await db.ClinicMercadoPagoAccounts.FindAsync(accountId);
            Assert.Equal(MercadoPagoAccountStatus.Revoked, account!.Status);
            Assert.Equal("refresh-old", cipher.Decrypt(account.RefreshTokenEncrypted));
        });
    }
}
