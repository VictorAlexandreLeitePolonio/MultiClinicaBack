using ProjetoLP.API.Common;
using ProjetoLP.API.Data;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.MedicalRecord;
using ProjetoLP.API.Models;
using ProjetoLP.API.Repositories.Interfaces;
using ProjetoLP.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ProjetoLP.API.Services;

public class MedicalRecordService(
    IMedicalRecordRepository repository,
    AppDbContext db) : IMedicalRecordService
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
        var userExists = await db.Users.AnyAsync(u => u.Id == dto.UserId);
        if (!userExists)
            return Result<MedicalRecordResponseDto>.Fail(ErrorCodes.NotFound, "Fisioterapeuta não encontrado.");

        var patientExists = await db.Patients.AnyAsync(p => p.Id == dto.PatientId);
        if (!patientExists)
            return Result<MedicalRecordResponseDto>.Fail(ErrorCodes.NotFound, "Paciente não encontrado.");

        var medicalRecord = new MedicalRecord
        {
            UserId               = dto.UserId,
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

    // ── Upload Contrato ──────────────────────────────────────────────────────

    public async Task<Result<string>> UploadContratoAsync(int id, Stream fileStream, string fileName, string contentType)
    {
        var record = await repository.GetByIdAsync(id);
        if (record is null)
            return Result<string>.Fail(ErrorCodes.NotFound, "Prontuário não encontrado.");

        if (contentType != "application/pdf")
            return Result<string>.Fail(ErrorCodes.InvalidFileType, "Apenas arquivos PDF são aceitos.");

        // Limite de 20MB para contratos
        if (fileStream.Length > 20 * 1024 * 1024)
            return Result<string>.Fail(ErrorCodes.FileTooLarge, "O arquivo não pode ser maior que 20MB.");

        var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var folder = Path.Combine("wwwroot", "uploads", "contratos");
        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, newFileName);

        using (var stream = File.Create(filePath))
        {
            await fileStream.CopyToAsync(stream);
        }

        record.Contrato = $"/uploads/contratos/{newFileName}";
        await repository.SaveChangesAsync();

        return Result<string>.Ok(record.Contrato);
    }

    // ── Upload Exame ─────────────────────────────────────────────────────────

    public async Task<Result<string>> UploadExameAsync(int id, Stream fileStream, string fileName, string contentType)
    {
        var record = await repository.GetByIdAsync(id);
        if (record is null)
            return Result<string>.Fail(ErrorCodes.NotFound, "Prontuário não encontrado.");

        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(contentType))
            return Result<string>.Fail(ErrorCodes.InvalidFileType, "Apenas imagens JPG ou PNG são aceitas.");

        // Limite de 10MB para imagens
        if (fileStream.Length > 10 * 1024 * 1024)
            return Result<string>.Fail(ErrorCodes.FileTooLarge, "A imagem não pode ser maior que 10MB.");

        var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        var folder = Path.Combine("wwwroot", "uploads", "exames");
        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, newFileName);

        using (var stream = File.Create(filePath))
        {
            await fileStream.CopyToAsync(stream);
        }

        record.ExamesImagem = $"/uploads/exames/{newFileName}";
        await repository.SaveChangesAsync();

        return Result<string>.Ok(record.ExamesImagem);
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
        UserId               = m.UserId,
        UserName             = m.User.Name,
        PatientId            = m.PatientId,
        PatientName          = m.Patient.Name,
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
