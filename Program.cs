using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Repositories;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

if (!builder.Environment.IsEnvironment("Testing"))
{
    var databaseUrl = builder.Configuration["DATABASE_URL"]
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DATABASE_URL não configurada.");

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(DatabaseUrl.ToNpgsqlConnectionString(databaseUrl)));
}

// Payment — Repository e Service
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();

// Financial — Repository e Service
builder.Services.AddScoped<IFinancialRepository, FinancialRepository>();
builder.Services.AddScoped<IFinancialService, FinancialService>();

// Patient — Repository e Service
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();

// Appointment — Repository e Service
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();

// Plan — Repository e Service
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IPlanService, PlanService>();

// User — Repository e Service
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUsuarioLogadoService, UsuarioLogadoService>();
builder.Services.AddScoped<IClinicaBillingService, ClinicaBillingService>();
builder.Services.AddScoped<IAttachmentStorage, R2AttachmentStorage>();

// MedicalRecord — Repository e Service
builder.Services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();

// Habilita o uso de Controllers com [ApiController] e roteamento automático.
// JsonStringEnumConverter garante que enums aparecem como "Scheduled" e não como 0.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<AppointmentStatusUpdater>();
builder.Services.AddHostedService<ClinicBillingBackgroundJob>();

// Lê as configurações JWT do appsettings.json.
// "!" suprime o aviso de nullable — garantimos que os valores existem no appsettings.
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

// Configura o esquema de autenticação JWT Bearer.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
         options.Events = new JwtBearerEvents
  {
        OnMessageReceived = context =>
        {
            context.Token = context.Request.Cookies["auth_token"];
            return Task.CompletedTask;
        }
  };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,           // Verifica se o token foi gerado por este servidor.
            ValidateAudience = true,         // Verifica se o token é destinado a este cliente.
            ValidateLifetime = true,         // Rejeita tokens expirados.
            ValidateIssuerSigningKey = true, // Verifica a assinatura do token com a chave secreta.
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = (builder.Configuration["CORS_ALLOWED_ORIGINS"]
                ?? builder.Configuration["Cors:AllowedOrigin"]
                ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});


// Constrói a aplicação — após essa linha, não é possível registrar novos serviços.
var app = builder.Build();

await AppBootstrapper.BootstrapSuperAdminAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ordem dos middlewares importa:
// 1. CORS — deve vir antes de Authentication/Authorization para permitir que o frontend envie o token.
// 2. StaticFiles — deve vir antes de Authentication/Authorization para servir arquivos públicos sem exigir autenticação.
// 3. Authentication — identifica quem é o usuário pelo token.
// 4. Authorization — verifica se o usuário tem permissão para o endpoint.
// 5. HttpsRedirection — redireciona HTTP para HTTPS.
app.UseCors("Frontend");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated != true
        || context.Request.Path.StartsWithSegments("/api/auth/login")
        || context.Request.Path.StartsWithSegments("/api/superadmin"))
    {
        await next();
        return;
    }

    var userIdValue = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!int.TryParse(userIdValue, out var userId))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { message = "Sessão inválida." });
        return;
    }

    var db = context.RequestServices.GetRequiredService<AppDbContext>();
    var user = await db.Users
        .Include(u => u.Clinica)
        .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

    if (user is null || !user.IsActive)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Usuário inativo." });
        return;
    }

    if (!user.Clinica.IsActive || user.Clinica.IsDeleted)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Clínica inativa." });
        return;
    }

    if (user.Clinica.IsBlockedByBilling && user.Role != UserRole.SuperAdmin)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { message = "Clínica bloqueada por pendência financeira." });
        return;
    }

    await next();
});
app.UseAuthorization();
app.UseHttpsRedirection();

// Mapeia automaticamente as rotas definidas nos Controllers.
app.MapControllers();

// Sobe o servidor.
app.Run();

internal static class AppBootstrapper
{
    public static async Task BootstrapSuperAdminAsync(WebApplication app)
    {
        var name = app.Configuration["SUPER_ADMIN_NAME"];
        var email = app.Configuration["SUPER_ADMIN_EMAIL"];
        var password = app.Configuration["SUPER_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password))
            return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminClinic = await db.Clinicas.FirstOrDefaultAsync(c => c.Nome == "Admin Interno");
        if (adminClinic is null)
        {
            adminClinic = new Clinica
            {
                Nome = "Admin Interno",
                NomeFantasia = "Admin Interno",
                NomeResponsavel = name.Trim(),
                Email = email.Trim().ToLowerInvariant(),
                IsActive = true,
                CobrancaAtiva = false
            };
            db.Clinicas.Add(adminClinic);
            await db.SaveChangesAsync();
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (!await db.Users.AnyAsync(u => u.Email == normalizedEmail && !u.IsDeleted))
        {
            db.Users.Add(new User
            {
                ClinicaId = adminClinic.Id,
                Name = name.Trim(),
                Email = normalizedEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.SuperAdmin,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }
    }
}

// Necessário para WebApplicationFactory nos testes de integração.
public partial class Program { }

internal static class DatabaseUrl
{
    public static string ToNpgsqlConnectionString(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            return value;

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty);
        var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty);
        var database = uri.AbsolutePath.TrimStart('/');

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = username,
            Password = password,
            Database = database,
            SslMode = uri.Query.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase)
                ? Npgsql.SslMode.Require
                : Npgsql.SslMode.Prefer
        };

        return builder.ConnectionString;
    }
}
