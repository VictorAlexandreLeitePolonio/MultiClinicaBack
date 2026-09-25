using System.Net.Mail;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Common;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Patient;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;
using Npgsql;

namespace MultiClinica.API.Services;

public class PatientImportService(
    AppDbContext db,
    IPatientAccountService accountService,
    IUsuarioLogadoService usuario,
    IConfiguration configuration,
    TimeProvider timeProvider) : IPatientImportService
{
    private const long DefaultMaxFileBytes = 10 * 1024 * 1024;
    private const int DefaultMaxRows = 10_000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly PatientImportFileParser Parser = new();

    public async Task<Result<PatientImportResponseDto>> ImportAsync(
        IFormFile? file,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(idempotencyKey, out var parsedKey) || parsedKey == Guid.Empty)
            return Result<PatientImportResponseDto>.Fail(ErrorCodes.InvalidFormat, "Idempotency-Key deve conter um UUID válido.");
        if (file is null)
            return Result<PatientImportResponseDto>.Fail(ErrorCodes.InvalidFormat, "Envie o arquivo no campo file.");

        var maxFileBytes = Math.Max(1, configuration.GetValue("PatientImport:MaxFileBytes", DefaultMaxFileBytes));
        if (file.Length > maxFileBytes)
            return Result<PatientImportResponseDto>.Fail(ErrorCodes.FileTooLarge, "O arquivo excede o tamanho máximo permitido.");

        byte[] bytes;
        try
        {
            bytes = await ReadFileAsync(file, maxFileBytes, cancellationToken);
        }
        catch (PatientImportParseException exception)
        {
            return Result<PatientImportResponseDto>.Fail(exception.Code, exception.Message);
        }

        IReadOnlyList<PatientImportSourceRow> rows;
        var maxRows = Math.Max(1, configuration.GetValue("PatientImport:MaxRows", DefaultMaxRows));
        try
        {
            rows = Parser.Parse(file.FileName, bytes, maxRows);
        }
        catch (PatientImportParseException exception)
        {
            return Result<PatientImportResponseDto>.Fail(exception.Code, exception.Message);
        }

        var key = parsedKey.ToString("D");
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        return await PersistAsync(rows, key, hash, cancellationToken);
    }

    private async Task<Result<PatientImportResponseDto>> PersistAsync(
        IReadOnlyList<PatientImportSourceRow> rows,
        string idempotencyKey,
        string fileHash,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = await FindOperationAsync(idempotencyKey, cancellationToken);
            if (existing is not null)
                return Replay(existing, fileHash);

            try
            {
                return await PersistAttemptAsync(rows, idempotencyKey, fileHash, cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                db.ChangeTracker.Clear();
                var concurrentOperation = await FindOperationAsync(idempotencyKey, cancellationToken);
                if (concurrentOperation is not null)
                    return Replay(concurrentOperation, fileHash);
                if (attempt == 1)
                    return Result<PatientImportResponseDto>.Fail(
                        ErrorCodes.IdempotencyConflict,
                        "O arquivo conflitou com outra importação. Reenvie a mesma tentativa.");
            }
        }

        return Result<PatientImportResponseDto>.Fail(ErrorCodes.IdempotencyConflict, "Não foi possível concluir a importação.");
    }

    private async Task<Result<PatientImportResponseDto>> PersistAttemptAsync(
        IReadOnlyList<PatientImportSourceRow> rows,
        string idempotencyKey,
        string fileHash,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var operation = new PatientImportOperation
        {
            ClinicaId = usuario.ClinicaId,
            IdempotencyKey = idempotencyKey,
            FileHash = fileHash,
            CreatedByUserId = usuario.UserId
        };
        db.PatientImportOperations.Add(operation);
        await db.SaveChangesAsync(cancellationToken);

        var (candidates, rejectedRows) = await PrepareRowsAsync(rows, cancellationToken);
        db.Patients.AddRange(candidates.Select(candidate => candidate.Patient));
        await db.SaveChangesAsync(cancellationToken);

        var response = new PatientImportResponseDto
        {
            ImportId = operation.ImportId,
            TotalRows = rows.Count,
            ImportedCount = candidates.Count,
            RejectedCount = rejectedRows.Count,
            EmailsSkippedNoEmailCount = candidates.Count(candidate => candidate.Account is null),
            Results = rejectedRows
        };

        var jobs = candidates
            .Where(candidate => candidate.Account is not null && candidate.NotificationType.HasValue)
            .Select(candidate => new PatientEmailOutbox
            {
                ClinicaId = usuario.ClinicaId,
                PatientId = candidate.Patient.Id,
                PatientAccountId = candidate.Account!.Id,
                Type = candidate.NotificationType!.Value,
                DeduplicationKey = $"{operation.ImportId:N}:{candidate.Patient.Id}",
                NextAttemptAt = timeProvider.GetUtcNow().UtcDateTime,
                CreatedByUserId = usuario.UserId
            })
            .ToArray();
        db.PatientEmailOutbox.AddRange(jobs);
        response.EmailsQueuedCount = jobs.Length;

        response.Results.AddRange(candidates.Select(candidate => new PatientImportRowResultDto
        {
            Row = candidate.Source.Row,
            Status = "Imported",
            PatientId = candidate.Patient.Id,
            EmailStatus = candidate.Account is null ? "SkippedNoEmail" : "Queued"
        }));
        response.Results = response.Results.OrderBy(result => result.Row).ToList();
        response.Status = response.RejectedCount > 0 ? "CompletedWithErrors" : "Completed";

        operation.Status = PatientImportStatus.Completed;
        operation.ResponseJson = JsonSerializer.Serialize(response, Json);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<PatientImportResponseDto>.Ok(response);
    }

    private async Task<(List<PatientImportCandidate> Candidates, List<PatientImportRowResultDto> Rejected)> PrepareRowsAsync(
        IReadOnlyList<PatientImportSourceRow> rows,
        CancellationToken cancellationToken)
    {
        var rejected = new List<PatientImportRowResultDto>();
        var timeZoneId = await db.Clinicas.Where(clinic => clinic.Id == usuario.ClinicaId)
            .Select(clinic => clinic.TimeZoneId).SingleAsync(cancellationToken);
        var today = PatientBirthDate.Today(timeProvider, timeZoneId);
        var drafts = BuildDrafts(rows, rejected, today);
        if (drafts.Count == 0)
            return ([], rejected);

        var identities = await LoadIdentityIndexAsync(drafts, cancellationToken);
        var candidates = new List<PatientImportCandidate>();
        var seenEmails = new HashSet<string>(StringComparer.Ordinal);
        var seenCpfs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var draft in drafts)
        {
            var errors = new List<PatientImportRowErrorDto>();
            var account = ResolveExistingAccount(draft, identities, seenEmails, seenCpfs, errors);
            if (errors.Count > 0)
            {
                rejected.Add(RejectedRow(draft.Source.Row, errors));
                continue;
            }

            candidates.Add(CreateCandidate(draft, account));
            if (draft.Email is not null)
                seenEmails.Add(draft.Email);
            if (draft.CPF is not null)
                seenCpfs.Add(draft.CPF);
        }

        return (candidates, rejected);
    }

    private List<PatientImportDraft> BuildDrafts(
        IReadOnlyList<PatientImportSourceRow> rows,
        List<PatientImportRowResultDto> rejected,
        DateOnly today)
    {
        var drafts = new List<PatientImportDraft>();
        foreach (var row in rows)
        {
            var errors = new List<PatientImportRowErrorDto>();
            var draft = TryCreateDraft(row, errors, today);
            if (draft is null)
                rejected.Add(RejectedRow(row.Row, errors));
            else
                drafts.Add(draft);
        }
        return drafts;
    }

    private PatientImportDraft? TryCreateDraft(
        PatientImportSourceRow row,
        List<PatientImportRowErrorDto> errors,
        DateOnly today)
    {
        var name = Optional(row.Name);
        if (name is null)
            errors.Add(RowError("Name", "EMPTY_FIELD", "Nome é obrigatório."));

        var email = accountService.NormalizeEmail(row.Email);
        if (email is not null && (!MailAddress.TryCreate(email, out var address)
            || !string.Equals(address!.Address, email, StringComparison.OrdinalIgnoreCase)))
            errors.Add(RowError("Email", "INVALID_FORMAT", "E-mail inválido."));

        var cpf = accountService.NormalizeCpf(row.CPF);
        ValidateDigits(row.CPF, cpf, "CPF", errors);
        var phone = DigitsOnly(row.Phone);
        ValidateDigits(row.Phone, phone, "Phone", errors);
        var cep = DigitsOnly(row.Cep);
        ValidateDigits(row.Cep, cep, "Cep", errors);
        DateOnly? birthDate = null;
        if (row.BirthDate is not null)
        {
            if (!DateOnly.TryParseExact(row.BirthDate, ["dd/MM/yyyy", "yyyy-MM-dd"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedBirthDate))
                errors.Add(RowError("BirthDate", ErrorCodes.InvalidDate, "Data de nascimento inválida. Use DD/MM/AAAA ou AAAA-MM-DD."));
            else if (parsedBirthDate > today)
                errors.Add(RowError("BirthDate", ErrorCodes.InvalidDate, "A data de nascimento não pode ser futura."));
            else
                birthDate = parsedBirthDate;
        }

        return errors.Count > 0
            ? null
            : new PatientImportDraft(
                row,
                name!,
                email,
                cpf,
                Optional(row.Rg),
                birthDate,
                Optional(row.Rua),
                Optional(row.Numero),
                Optional(row.Bairro),
                Optional(row.Cidade),
                Optional(row.Estado),
                cep,
                phone);
    }

    private async Task<PatientImportIdentityIndex> LoadIdentityIndexAsync(
        IReadOnlyList<PatientImportDraft> drafts,
        CancellationToken cancellationToken)
    {
        var requestedEmails = drafts.Where(draft => draft.Email is not null)
            .Select(draft => draft.Email!).Distinct(StringComparer.Ordinal).ToArray();
        var requestedCpfs = drafts.Where(draft => draft.CPF is not null)
            .Select(draft => draft.CPF!).Distinct(StringComparer.Ordinal).ToArray();

        var existingPatients = await db.Patients
            .Where(patient => patient.ClinicaId == usuario.ClinicaId && !patient.IsDeleted
                && ((patient.Email != null && requestedEmails.Contains(patient.Email.Trim().ToLower()))
                    || (patient.CPF != null && requestedCpfs.Contains(patient.CPF))))
            .ToListAsync(cancellationToken);

        var accounts = await db.PatientAccounts
            .Where(account => !account.IsDeleted
                && ((account.Email != null && requestedEmails.Contains(account.Email.Trim().ToLower()))
                    || (account.CPF != null && requestedCpfs.Contains(account.CPF))))
            .ToListAsync(cancellationToken);
        var accountsByEmail = accounts.Where(account => accountService.NormalizeEmail(account.Email) is not null)
            .GroupBy(account => accountService.NormalizeEmail(account.Email)!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var accountsByCpf = accounts.Where(account => accountService.NormalizeCpf(account.CPF) is not null)
            .GroupBy(account => accountService.NormalizeCpf(account.CPF)!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var matchingAccountIds = accounts.Select(account => account.Id).ToArray();
        var alreadyLinkedAccountIds = matchingAccountIds.Length == 0
            ? []
            : await db.Patients
                .Where(patient => patient.ClinicaId == usuario.ClinicaId && !patient.IsDeleted
                    && patient.PatientAccountId.HasValue
                    && matchingAccountIds.Contains(patient.PatientAccountId.Value))
                .Select(patient => patient.PatientAccountId!.Value)
                .ToListAsync(cancellationToken);

        return new PatientImportIdentityIndex(
            accountsByEmail,
            accountsByCpf,
            existingPatients.Where(patient => patient.Email is not null)
                .Select(patient => accountService.NormalizeEmail(patient.Email)!)
                .ToHashSet(StringComparer.Ordinal),
            existingPatients.Where(patient => patient.CPF is not null)
                .Select(patient => accountService.NormalizeCpf(patient.CPF)!)
                .ToHashSet(StringComparer.Ordinal),
            alreadyLinkedAccountIds.ToHashSet());
    }

    private PatientAccount? ResolveExistingAccount(
        PatientImportDraft draft,
        PatientImportIdentityIndex identities,
        HashSet<string> seenEmails,
        HashSet<string> seenCpfs,
        List<PatientImportRowErrorDto> errors)
    {
        if (draft.Email is not null)
        {
            if (identities.LocalEmails.Contains(draft.Email))
                errors.Add(RowError("Email", ErrorCodes.DuplicateEmail, "E-mail já cadastrado nesta clínica."));
            else if (seenEmails.Contains(draft.Email))
                errors.Add(RowError("Email", "DUPLICATE_IN_FILE", "E-mail repetido no arquivo."));
        }
        if (draft.CPF is not null)
        {
            if (identities.LocalCpfs.Contains(draft.CPF))
                errors.Add(RowError("CPF", ErrorCodes.DuplicateCpf, "CPF já cadastrado nesta clínica."));
            else if (seenCpfs.Contains(draft.CPF))
                errors.Add(RowError("CPF", "DUPLICATE_IN_FILE", "CPF repetido no arquivo."));
        }

        PatientAccount? account = null;
        if (draft.Email is not null && identities.AccountsByEmail.TryGetValue(draft.Email, out var emailMatches))
        {
            if (emailMatches.Length != 1)
                errors.Add(RowError("Email", "IDENTITY_CONFLICT", "Não foi possível resolver a identidade informada."));
            else
                account = emailMatches[0];
        }

        var cpfMatches = draft.CPF is not null && identities.AccountsByCpf.TryGetValue(draft.CPF, out var matches)
            ? matches
            : [];
        if (account is not null)
        {
            if (draft.CPF is not null && accountService.NormalizeCpf(account.CPF) is { } accountCpf
                && accountCpf != draft.CPF)
                errors.Add(RowError("CPF", "IDENTITY_CONFLICT", "E-mail e CPF não correspondem à mesma identidade."));
            if (cpfMatches.Any(match => match.Id != account.Id))
                errors.Add(RowError("CPF", "IDENTITY_CONFLICT", "E-mail e CPF não correspondem à mesma identidade."));
            if (identities.AlreadyLinkedAccountIds.Contains(account.Id))
                errors.Add(RowError("Email", ErrorCodes.AlreadyLinked, "Esta identidade já está vinculada a esta clínica."));
        }
        else if (cpfMatches.Length > 0)
        {
            errors.Add(RowError("CPF", ErrorCodes.DuplicateCpf, "CPF já vinculado a outra identidade."));
        }

        return account;
    }

    private PatientImportCandidate CreateCandidate(PatientImportDraft draft, PatientAccount? account)
    {
        var createAccount = draft.Email is not null && account is null;
        if (createAccount)
            account = accountService.CreatePending(draft.Name, draft.Email, draft.CPF, draft.Phone, usuario.UserId);

        var patient = new Patient
        {
            ClinicaId = usuario.ClinicaId,
            Name = draft.Name,
            Email = draft.Email,
            CPF = draft.CPF,
            Rg = draft.Rg,
            BirthDate = draft.BirthDate,
            Rua = draft.Rua,
            Numero = draft.Numero,
            Bairro = draft.Bairro,
            Cidade = draft.Cidade,
            Estado = draft.Estado,
            Cep = draft.Cep,
            Phone = draft.Phone,
            CreatedByUserId = usuario.UserId
        };

        if (account is not null)
        {
            if (createAccount)
                patient.PatientAccount = account;
            else
                patient.PatientAccountId = account.Id;
        }

        PatientEmailOutboxType? notificationType = account?.Status == PatientAccountStatus.PendingActivation
            ? PatientEmailOutboxType.ActivationInvitation
            : account is null ? null : PatientEmailOutboxType.ClinicLinkNotice;
        return new PatientImportCandidate(draft.Source, patient, account, notificationType);
    }

    private async Task<byte[]> ReadFileAsync(IFormFile file, long maxFileBytes, CancellationToken cancellationToken)
    {
        await using var input = file.OpenReadStream();
        using var output = new MemoryStream();
        var buffer = new byte[81_920];
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
                break;
            if (output.Length + read > maxFileBytes)
                throw new PatientImportParseException(ErrorCodes.FileTooLarge, "O arquivo excede o tamanho máximo permitido.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private Task<PatientImportOperation?> FindOperationAsync(string key, CancellationToken cancellationToken)
        => db.PatientImportOperations.AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.ClinicaId == usuario.ClinicaId
                && operation.IdempotencyKey == key, cancellationToken);

    private static Result<PatientImportResponseDto> Replay(PatientImportOperation operation, string fileHash)
    {
        if (!string.Equals(operation.FileHash, fileHash, StringComparison.Ordinal))
            return Result<PatientImportResponseDto>.Fail(
                ErrorCodes.IdempotencyConflict,
                "Esta chave de idempotência já foi usada com outro arquivo.");
        if (operation.Status != PatientImportStatus.Completed || operation.ResponseJson is null)
            return Result<PatientImportResponseDto>.Fail(
                ErrorCodes.IdempotencyConflict,
                "A importação desta chave ainda está em processamento. Repita a mesma tentativa.");

        var response = JsonSerializer.Deserialize<PatientImportResponseDto>(operation.ResponseJson, Json);
        return response is null
            ? Result<PatientImportResponseDto>.Fail(ErrorCodes.InvalidFormat, "Não foi possível recuperar o resultado da importação.")
            : Result<PatientImportResponseDto>.Ok(response);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static PatientImportRowErrorDto RowError(string field, string code, string message)
        => new() { Field = field, Code = code, Message = message };

    private static PatientImportRowResultDto RejectedRow(int row, List<PatientImportRowErrorDto> errors)
        => new() { Row = row, Status = "Rejected", Errors = errors };

    private static string? Optional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? DigitsOnly(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsDigit).ToArray());

    private static void ValidateDigits(string? source, string? normalized, string field, List<PatientImportRowErrorDto> errors)
    {
        if (!string.IsNullOrWhiteSpace(source) && string.IsNullOrEmpty(normalized))
            errors.Add(RowError(field, "INVALID_FORMAT", $"{field} informado não contém dígitos válidos."));
    }

    private sealed record PatientImportDraft(
        PatientImportSourceRow Source,
        string Name,
        string? Email,
        string? CPF,
        string? Rg,
        DateOnly? BirthDate,
        string? Rua,
        string? Numero,
        string? Bairro,
        string? Cidade,
        string? Estado,
        string? Cep,
        string? Phone);

    private sealed record PatientImportIdentityIndex(
        Dictionary<string, PatientAccount[]> AccountsByEmail,
        Dictionary<string, PatientAccount[]> AccountsByCpf,
        HashSet<string> LocalEmails,
        HashSet<string> LocalCpfs,
        HashSet<int> AlreadyLinkedAccountIds);

    private sealed record PatientImportCandidate(
        PatientImportSourceRow Source,
        Patient Patient,
        PatientAccount? Account,
        PatientEmailOutboxType? NotificationType);
}
