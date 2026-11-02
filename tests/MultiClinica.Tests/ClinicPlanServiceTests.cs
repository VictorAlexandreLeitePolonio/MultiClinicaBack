using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using Xunit;

namespace MultiClinica.Tests;

public class ClinicPlanServiceTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static async Task<Clinica> SeedAsync(AppDbContext db, ClinicPlan plan, int professionals)
    {
        var clinic = new Clinica { Nome = "C", Plan = plan };
        db.Clinicas.Add(clinic);
        await db.SaveChangesAsync();
        for (var i = 0; i < professionals; i++)
            db.Users.Add(new User
            {
                ClinicaId = clinic.Id, Name = $"P{i}", Email = $"p{i}@x.com", Role = UserRole.Profissional,
                CreatedAt = DateTime.UtcNow.AddMinutes(i),
            });
        await db.SaveChangesAsync();
        return clinic;
    }

    [Fact]
    public async Task Free_aceita_um_profissional()
    {
        await using var db = NewDb();
        var clinic = await SeedAsync(db, ClinicPlan.Free, 0);
        var service = new ClinicPlanService(db);
        Assert.True(await service.CanAddProfessionalAsync(clinic.Id));
        await SeedAsync(db, ClinicPlan.Free, 0); // outra clínica não interfere
        db.Users.Add(new User { ClinicaId = clinic.Id, Name = "A", Email = "a@x.com", Role = UserRole.Profissional });
        await db.SaveChangesAsync();
        Assert.False(await service.CanAddProfessionalAsync(clinic.Id));
    }

    [Fact]
    public async Task Rebaixar_desativa_os_mais_recentes_acima_do_limite()
    {
        await using var db = NewDb();
        var clinic = await SeedAsync(db, ClinicPlan.Premium, 5);
        var service = new ClinicPlanService(db);

        await service.ApplyPlanAsync(clinic, ClinicPlan.Free, null);
        await db.SaveChangesAsync();

        var active = await db.Users.Where(u => u.ClinicaId == clinic.Id && u.IsActive).Select(u => u.Name).ToListAsync();
        Assert.Equal(["P0"], active);
        Assert.Equal(ClinicPlan.Free, clinic.Plan);
        Assert.Null(clinic.BillingCycle);
        Assert.Contains(db.CommercialHistoryEvents, e => e.Type == CommercialHistoryEventType.ProfessionalsDeactivatedByPlan);
    }

    [Fact]
    public async Task Vaga_comprada_conta_no_limite_ao_rebaixar()
    {
        await using var db = NewDb();
        var clinic = await SeedAsync(db, ClinicPlan.Pro, 3);
        clinic.ExtraProfessionals = 2;
        var service = new ClinicPlanService(db);

        await service.ApplyPlanAsync(clinic, ClinicPlan.Free, null);
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Users.CountAsync(u => u.ClinicaId == clinic.Id && u.IsActive)); // 1 incluído + 1 vaga
    }

    [Fact]
    public async Task Subir_de_plano_nao_desativa_ninguem()
    {
        await using var db = NewDb();
        var clinic = await SeedAsync(db, ClinicPlan.Free, 1);
        await new ClinicPlanService(db).ApplyPlanAsync(clinic, ClinicPlan.Pro, BillingCycle.Annual);
        await db.SaveChangesAsync();
        Assert.Equal(ClinicPlan.Pro, clinic.Plan);
        Assert.Equal(BillingCycle.Annual, clinic.BillingCycle);
        Assert.True(await db.Users.AllAsync(u => u.IsActive));
    }
}
