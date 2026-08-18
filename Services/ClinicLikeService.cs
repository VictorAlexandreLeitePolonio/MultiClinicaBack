using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

public class ClinicLikeService(AppDbContext db, IPatientAccountLoggedService patient) : IClinicLikeService
{
    public async Task<Result<ClinicLikeResult>> LikeAsync(int clinicId)
    {
        var clinic = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == clinicId && c.IsActive && !c.IsDeleted);
        if (clinic is null)
            return Result<ClinicLikeResult>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        var existing = await db.ClinicLikes
            .AnyAsync(l => l.PatientAccountId == patient.PatientAccountId && l.ClinicaId == clinicId);

        // Idempotente: like repetido não incrementa novamente.
        if (!existing)
        {
            db.ClinicLikes.Add(new ClinicLike
            {
                PatientAccountId = patient.PatientAccountId,
                ClinicaId        = clinicId,
            });
            clinic.LikeCount += 1;
            await db.SaveChangesAsync(); // insert + incremento na mesma transação
        }

        return Result<ClinicLikeResult>.Ok(new ClinicLikeResult(clinicId, clinic.LikeCount, true));
    }

    public async Task<Result<ClinicLikeResult>> UnlikeAsync(int clinicId)
    {
        var clinic = await db.Clinicas.FirstOrDefaultAsync(c => c.Id == clinicId && !c.IsDeleted);
        if (clinic is null)
            return Result<ClinicLikeResult>.Fail(ErrorCodes.NotFound, "Clínica não encontrada.");

        var like = await db.ClinicLikes
            .FirstOrDefaultAsync(l => l.PatientAccountId == patient.PatientAccountId && l.ClinicaId == clinicId);

        // Unlike inexistente é no-op e nunca leva o contador abaixo de zero.
        if (like is not null)
        {
            db.ClinicLikes.Remove(like);
            clinic.LikeCount = Math.Max(0, clinic.LikeCount - 1);
            await db.SaveChangesAsync(); // delete + decremento na mesma transação
        }

        return Result<ClinicLikeResult>.Ok(new ClinicLikeResult(clinicId, clinic.LikeCount, false));
    }
}
