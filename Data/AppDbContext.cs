// Importa o namespace do EF Core — necessário para usar DbContext, DbSet, etc.
using Microsoft.EntityFrameworkCore;

// Importa os Models — necessário para referenciar User, Appointment, etc.
using ProjetoLP.API.Models;

// Namespace da pasta Data — convenção de organização do projeto.
namespace ProjetoLP.API.Data;

// AppDbContext é a classe central do EF Core.
// Representa o banco de dados inteiro — é por aqui que todas as operações passam.
// "DbContext" é a classe base do EF Core que fornece toda a funcionalidade.
public class AppDbContext : DbContext
{
    // Construtor — recebe as opções de configuração (qual banco usar, connection string, etc.)
    // e repassa para a classe base via ": base(options)".
    // É chamado automaticamente pelo sistema de injeção de dependência do .NET.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // DbSet representa uma tabela no banco.
    // "Users" será o nome da tabela — o EF Core usa o nome da propriedade no plural.
    // Permite operações: db.Users.ToList(), db.Users.FindAsync(id), etc.
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<MedicalRecord> MedicalRecords { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<Plans>       Plans        { get; set; } = null!;
    public DbSet<Expense>     Expenses     { get; set; } = null!;
    public DbSet<WhatsAppLog> WhatsAppLogs { get; set; } = null!;

    // Método chamado automaticamente pelo EF Core ao criar o banco.
    // É aqui que configuramos os relacionamentos entre as tabelas explicitamente.
    // "override" significa que estamos sobrescrevendo o comportamento padrão da classe base.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Relacionamento User → Appointments (1:N)
        // Lê-se: "Um User tem muitos Appointments.
        //         Cada Appointment pertence a um User.
        //         A chave estrangeira no banco é a coluna UserId."
        modelBuilder.Entity<User>()
            .HasMany(u => u.Appointments)   // User tem muitos Appointments
            .WithOne(a => a.User)           // Appointment pertence a um User
            .HasForeignKey(a => a.UserId);  // FK que liga as tabelas

        // Relacionamento User → MedicalRecords (1:N)
        modelBuilder.Entity<User>()
            .HasMany(u => u.MedicalRecords)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId);

        // Relacionamento User → Payments (1:N)
        modelBuilder.Entity<User>()
            .HasMany(u => u.Payments)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId);

        // — Patient → Appointments (1:N)                                                                                                                                                                  
        modelBuilder.Entity<Patient>()
            .HasMany(p => p.Appointments)
            .WithOne(a => a.Patient)
            .HasForeignKey(a => a.PatientId);

        // Relacionamento Patient → MedicalRecords (1:N)
        modelBuilder.Entity<Patient>()
            .HasMany(p => p.MedicalRecords)
            .WithOne(m => m.Patient)
            .HasForeignKey(m => m.PatientId);

        modelBuilder.Entity<Patient>()
            .HasMany(p => p.Payments)
            .WithOne(p => p.Patient)
            .HasForeignKey(p => p.PatientId);

        // Payment → Plans (N:1) — cada pagamento pertence a um plano.
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Plan)
            .WithMany(pl => pl.Payments)
            .HasForeignKey(p => p.PlanId);

        // WhatsAppLog → Patient (N:1, opcional) — log pode existir sem paciente vinculado.
        modelBuilder.Entity<WhatsAppLog>()
            .HasOne(w => w.Patient)
            .WithMany()
            .HasForeignKey(w => w.PatientId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
