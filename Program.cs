using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.Services;
using ProjetoLP.API.Repositories;
using ProjetoLP.API.Repositories.Interfaces;
using ProjetoLP.API.Services.Interfaces;

// Cria o construtor da aplicação — todos os serviços são registrados aqui antes do Build().
var builder = WebApplication.CreateBuilder(args);

// Registra o AppDbContext com SQLite via string de conexão do appsettings.json.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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

// MedicalRecord — Repository e Service
builder.Services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
builder.Services.AddScoped<IMedicalRecordService, MedicalRecordService>();

// Habilita o uso de Controllers com [ApiController] e roteamento automático.
// JsonStringEnumConverter garante que enums aparecem como "Scheduled" e não como 0.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Configura o Swagger para documentação da API.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Serviço em segundo plano — cancela consultas Scheduled com data no passado.
builder.Services.AddHostedService<AppointmentStatusUpdater>();

// WhatsApp — cliente HTTP tipado para a Evolution API.
builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["EvolutionApi:BaseUrl"]!);
    client.DefaultRequestHeaders.Add("apikey", builder.Configuration["EvolutionApi:ApiKey"]!);
});

// Serviço em segundo plano — envia lembretes de consulta via WhatsApp 24h antes.
builder.Services.AddHostedService<AppointmentReminderJob>();

// Serviço em segundo plano — envia lembretes de vencimento de pagamento via WhatsApp 24h antes.
builder.Services.AddHostedService<PaymentReminderJob>();

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
          policy.WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // necessário para enviar/receber cookies
      });
  });


// Constrói a aplicação — após essa linha, não é possível registrar novos serviços.
var app = builder.Build();


    app.UseSwagger();
    app.UseSwaggerUI();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Aplica migrations pendentes.
    db.Database.Migrate();


    // ── Usuário Admin ────────────────────────────────────────────────────────
    if (!db.Users.Any(u => u.Email == "admin@clinica.com"))
    {
        db.Users.Add(new User
        {
            Name         = "Admin",
            Email        = "admin@clinica.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role         = UserRole.Admin
        });
        db.SaveChanges();
    }
}


// Ordem dos middlewares importa:
// 1. CORS — deve vir antes de Authentication/Authorization para permitir que o frontend envie o token.
// 2. StaticFiles — deve vir antes de Authentication/Authorization para servir arquivos públicos sem exigir autenticação.
// 3. Authentication — identifica quem é o usuário pelo token.
// 4. Authorization — verifica se o usuário tem permissão para o endpoint.
// 5. HttpsRedirection — redireciona HTTP para HTTPS.
app.UseCors("Frontend");
app.UseStaticFiles(); // Habilita servir arquivos estáticos (ex: imagens, CSS, JS) da pasta wwwroot.
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

// Mapeia automaticamente as rotas definidas nos Controllers.
app.MapControllers();

// Sobe o servidor.
app.Run();

// Necessário para WebApplicationFactory nos testes de integração.
public partial class Program { }
