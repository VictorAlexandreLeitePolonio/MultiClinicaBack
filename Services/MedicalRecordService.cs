using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.MedicalRecord;
using MultiClinica.API.Models;
using MultiClinica.API.Repositories.Interfaces;
using MultiClinica.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MultiClinica.API.Services;

public class MedicalRecordService(
    IMedicalRecordRepository repository,
    AppDbContext db,
    IUsuarioLogadoService usuario) : IMedicalRecordService
{
    // ── Listagem ─────────────────────────────────────────────────────────────

    public async Task<Result<PagedResult<MedicalRecordResponseDto>>> GetPagedAsync(
        int? patientId, string? patientName, int? userId,
        DateOnly? createdAt, int page, int pageSize)
    {
        var (items, total) = await repository.GetPagedAsync(
            patientId, patientName, userId, createdAt, page, pageSize);

        var data = items.Select(ToDto);

        return Result<PagedResult<MedicalRecordResponseDto>>.Ok(new PagedResult<MedicalRecordResponseDto>
        {
            Data       = data,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // ── Busca por Id ─────────────────────────────────────────────────────────

    public async Task<Result<MedicalRecordResponseDto>> GetByIdAsync(int id)
    {
        var record = await repository.GetByIdAsync(id);
        if (record is null)
            return Result<MedicalRecordResponseDto>.Fail(ErrorCodes.NotFound, "Prontuário não encontrado.");

        return Result<MedicalRecordResponseDto>.Ok(ToDto(record));
    }

    // ── Criação ──────────────────────────────────────────────────────────────

    public async Task<Result<MedicalRecordResponseDto>> CreateAsync(CreateMedicalRecordDto dto)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == dto.ProfessionalId && u.ClinicaId == usuario.ClinicaId && !u.IsDeleted);
        if (!userExists)
            return Result<MedicalRecordResponseDto>.Fail(ErrorCodes.NotFound, "Fisioterapeuta não encontrado.");

        var patientExists = await db.Patients.AnyAsync(p => p.Id == dto.PatientId && p.ClinicaId == usuario.ClinicaId && !p.IsDeleted);
        if (!patientExists)
            return Result<MedicalRecordResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        var medicalRecord = new MedicalRecord
        {
            ClinicaId            = usuario.ClinicaId,
            UserId               = dto.ProfessionalId,
            PatientId            = dto.PatientId,
            Patologia            = dto.Patologia,
            QueixaPrincipal      = dto.QueixaPrincipal,
            DoencaAntiga         = dto.DoencaAntiga,
            DoencaAtual          = dto.DoencaAtual,
            Habitos              = dto.Habitos,
            ExamesFisicos        = dto.ExamesFisicos,
            SinaisVitais         = dto.SinaisVitais,
            Medicamentos         = dto.Medicamentos,
            Cirurgias            = dto.Cirurgias,
            OutrasDoencas        = dto.OutrasDoencas,
            Sessao               = dto.Sessao,
            Titulo               = dto.Titulo,
            OrientacaoDomiciliar = dto.OrientacaoDomiciliar,
            CreatedByUserId      = usuario.UserId,
        };

        await repository.AddAsync(medicalRecord);

        // Recarrega com as navigation properties preenchidas
        var created = await repository.GetByIdAsync(medicalRecord.Id);
        return Result<MedicalRecordResponseDto>.Ok(ToDto(created!));
    }

    // ── Atualização ──────────────────────────────────────────────────────────

    public async Task<Result<MedicalRecordResponseDto>> UpdateAsync(int id, UpdateMedicalRecordDto dto)
    {
        var record = await repository.GetByIdAsync(id);
        if (record is null)
            return Result<MedicalRecordResponseDto>.Fail(ErrorCodes.NotFound, "Prontuário não encontrado.");

        record.Patologia            = dto.Patologia;
        record.QueixaPrincipal      = dto.QueixaPrincipal;
        record.DoencaAntiga         = dto.DoencaAntiga;
        record.DoencaAtual          = dto.DoencaAtual;
        record.Habitos              = dto.Habitos;
        record.ExamesFisicos        = dto.ExamesFisicos;
        record.SinaisVitais         = dto.SinaisVitais;
        record.Medicamentos         = dto.Medicamentos;
        record.Cirurgias            = dto.Cirurgias;
        record.OutrasDoencas        = dto.OutrasDoencas;
        record.Sessao               = dto.Sessao;
        record.Titulo               = dto.Titulo;
        record.OrientacaoDomiciliar = dto.OrientacaoDomiciliar;

        await repository.SaveChangesAsync();

        return Result<MedicalRecordResponseDto>.Ok(ToDto(record));
    }

    // ── Deleção ──────────────────────────────────────────────────────────────

    public async Task<Result<bool>> DeleteAsync(int id)
    {
        var record = await repository.GetByIdAsync(id);
        if (record is null)
            return Result<bool>.Fail(ErrorCodes.NotFound, "Prontuário não encontrado.");

        await repository.DeleteAsync(record);
        return Result<bool>.Ok(true);
    }

    // ── Mapeamento ───────────────────────────────────────────────────────────

    private static MedicalRecordResponseDto ToDto(MedicalRecord m) => new()
    {
        Id                   = m.Id,
        ProfessionalId       = m.UserId,
        UserName             = m.User.Name,
        PatientId            = m.PatientId,
        PatientName          = m.Patient.Name ?? string.Empty,
        Patologia            = m.Patologia,
        QueixaPrincipal      = m.QueixaPrincipal,
        ExamesImagem         = m.ExamesImagem,
        DoencaAntiga         = m.DoencaAntiga,
        DoencaAtual          = m.DoencaAtual,
        Habitos              = m.Habitos,
        ExamesFisicos        = m.ExamesFisicos,
        SinaisVitais         = m.SinaisVitais,
        Medicamentos         = m.Medicamentos,
        Cirurgias            = m.Cirurgias,
        OutrasDoencas        = m.OutrasDoencas,
        Sessao               = m.Sessao,
        Titulo               = m.Titulo,
        Contrato             = m.Contrato,
        OrientacaoDomiciliar = m.OrientacaoDomiciliar,
        CreatedAt            = m.CreatedAt,
    };
}
