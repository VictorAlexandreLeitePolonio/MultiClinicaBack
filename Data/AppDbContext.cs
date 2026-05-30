using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Models;

namespace MultiClinica.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Clinica> Clinicas { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Appointment> Appointments { get; set; } = null!;
    public DbSet<MedicalRecord> MedicalRecords { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<Patient> Patients { get; set; } = null!;
    public DbSet<Plans> Plans { get; set; } = null!;
    public DbSet<Expense> Expenses { get; set; } = null!;
    public DbSet<ClinicCharge> ClinicCharges { get; set; } = null!;
    public DbSet<CommercialHistoryEvent> CommercialHistoryEvents { get; set; } = null!;
    public DbSet<ClinicalAttachment> ClinicalAttachments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clinica>()
            .HasIndex(c => c.Cnpj);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<Appointment>()
            .Property(a => a.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Payment>()
            .Property(p => p.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ClinicCharge>()
            .Property(c => c.Status)
            .HasConversion<string>();

        modelBuilder.Entity<CommercialHistoryEvent>()
            .Property(c => c.Type)
            .HasConversion<string>();

        modelBuilder.Entity<ClinicalAttachment>()
            .Property(a => a.Type)
            .HasConversion<string>();

        modelBuilder.Entity<User>()
            .HasOne(u => u.Clinica)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.ClinicaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Appointments)
            .WithOne(a => a.User)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.MedicalRecords)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Payments)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Patient>()
            .HasOne(p => p.Clinica)
            .WithMany(c => c.Patients)
            .HasForeignKey(p => p.ClinicaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Patient>()
            .HasMany(p => p.Appointments)
            .WithOne(a => a.Patient)
            .HasForeignKey(a => a.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Patient>()
            .HasMany(p => p.MedicalRecords)
            .WithOne(m => m.Patient)
            .HasForeignKey(m => m.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Patient>()
            .HasMany(p => p.Payments)
            .WithOne(p => p.Patient)
            .HasForeignKey(p => p.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Plan)
            .WithMany(pl => pl.Payments)
            .HasForeignKey(p => p.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ClinicalAttachment>()
            .HasOne(a => a.MedicalRecord)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MedicalRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAudit();
        return base.SaveChanges();
    }

    private void ApplyAudit()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
    }
}
