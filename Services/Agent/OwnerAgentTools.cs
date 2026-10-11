using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Appointment;
using MultiClinica.API.DTOs.Patient;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services.Agent;

// Tools do dono. O tenant vem do IUsuarioLogadoService (preenchido via AgentIdentity), nunca dos argumentos.
public sealed class OwnerAgentTools(
    AppDbContext db,
    IAppointmentService appointments,
    IPatientService patients,
    IUsuarioLogadoService usuario,
    ILogger<OwnerAgentTools> logger)
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private const int MaxSlotDays = 7;

    public static readonly IReadOnlyList<AgentToolDefinition> Definitions =
    [
        new("buscar_paciente", "Busca pacientes da clínica pelo nome (parcial, sem diferenciar maiúsculas). Retorna até 5.",
            """{"type":"object","properties":{"nome":{"type":"string"}},"required":["nome"]}"""),
        new("cadastrar_paciente", "Cadastra um paciente novo. Todos os campos são obrigatórios; se a conta do paciente for nova, ele recebe e-mail de convite de ativação.",
            """{"type":"object","properties":{"nome":{"type":"string"},"telefone":{"type":"string"},"email":{"type":"string"},"cpf":{"type":"string"}},"required":["nome","telefone","email","cpf"]}"""),
        new("ver_agenda", "Lista consultas agendadas entre duas datas (inclusive), opcionalmente de um profissional.",
            """{"type":"object","properties":{"data_inicio":{"type":"string","description":"yyyy-MM-dd"},"data_fim":{"type":"string","description":"yyyy-MM-dd"},"profissional_id":{"type":"integer"}},"required":["data_inicio","data_fim"]}"""),
        new("consultar_horarios", "Lista horários livres por profissional a partir de data_inicio (até 7 dias). Sem profissional_id, traz todos.",
            """{"type":"object","properties":{"data_inicio":{"type":"string","description":"yyyy-MM-dd"},"data_fim":{"type":"string","description":"yyyy-MM-dd"},"profissional_id":{"type":"integer"}},"required":["data_inicio"]}"""),
        new("agendar", "Agenda uma consulta.",
            """{"type":"object","properties":{"paciente_id":{"type":"integer"},"profissional_id":{"type":"integer"},"data_hora":{"type":"string","description":"yyyy-MM-ddTHH:mm, horário local"}},"required":["paciente_id","profissional_id","data_hora"]}"""),
        new("remarcar", "Muda data/hora de uma consulta agendada (mesmo profissional).",
            """{"type":"object","properties":{"consulta_id":{"type":"integer"},"nova_data_hora":{"type":"string","description":"yyyy-MM-ddTHH:mm, horário local"}},"required":["consulta_id","nova_data_hora"]}"""),
        new("cancelar", "Cancela uma consulta.",
            """{"type":"object","properties":{"consulta_id":{"type":"integer"}},"required":["consulta_id"]}"""),
    ];

    public async Task<string> ExecuteAsync(string name, string argumentsJson)
    {
        using var doc = TryParse(argumentsJson);
        if (doc is null)
            return Error("Argumentos em JSON inválido.");
        var args = doc.RootElement;

        try
        {
            return name switch
            {
                "buscar_paciente" => await SearchPatientAsync(Str(args, "nome")),
                "cadastrar_paciente" => await CreatePatientAsync(args),
                "ver_agenda" => await AgendaAsync(args),
                "consultar_horarios" => await FreeSlotsAsync(args),
                "agendar" => await ScheduleAsync(args),
                "remarcar" => await RescheduleAsync(Int(args, "consulta_id"), Str(args, "nova_data_hora")),
                "cancelar" => await CancelAsync(Int(args, "consulta_id")),
                _ => Error($"Ferramenta desconhecida: {name}."),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            return Error(ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Fronteira do agente: o modelo recebe um erro genérico, o detalhe fica no log.
            logger.LogError(ex, "Falha na tool {Tool}", name);
            return Error("Erro interno ao executar a ação.");
        }
    }

    private async Task<string> SearchPatientAsync(string name)
    {
        var term = name.Trim().ToLower();
        var found = await db.Patients.AsNoTracking()
            .Where(p => p.ClinicaId == usuario.ClinicaId && !p.IsDeleted && p.Name != null && p.Name.ToLower().Contains(term))
            .OrderBy(p => p.Name).Take(5)
            .Select(p => new { id = p.Id, nome = p.Name, telefone = p.Phone })
            .ToListAsync();
        return Ok(new { ok = true, pacientes = found });
    }

    private async Task<string> CreatePatientAsync(JsonElement args)
    {
        var result = await patients.CreateAsync(new CreatePatientDto
        {
            Name = Str(args, "nome"),
            Phone = Str(args, "telefone"),
            Email = Str(args, "email"),
            CPF = Str(args, "cpf"),
        });
        return result.IsSuccess
            ? Ok(new { ok = true, paciente_id = result.Value!.PatientId, convite_enviado = result.Value.InvitationSent })
            : Error(result.ErrorMessage!);
    }

    private async Task<string> AgendaAsync(JsonElement args)
    {
        var tz = await ClinicZoneAsync();
        var (start, end) = AgentTime.DayRangeUtc(AgentTime.ParseDate(Str(args, "data_inicio")),
            AgentTime.ParseDate(Str(args, "data_fim")), tz);
        var professionalId = OptInt(args, "profissional_id");
        var items = await db.Appointments.AsNoTracking().Include(a => a.Patient).Include(a => a.User)
            .Where(a => a.ClinicaId == usuario.ClinicaId && !a.IsDeleted && a.Status == AppointmentStatus.Scheduled
                && a.AppointmentDate >= start && a.AppointmentDate < end
                && (professionalId == null || a.UserId == professionalId))
            .OrderBy(a => a.AppointmentDate).Take(100)
            .ToListAsync();
        return Ok(new
        {
            ok = true,
            consultas = items.Select(a => new
            {
                consulta_id = a.Id,
                data_hora = AgentTime.ToLocalIso(a.AppointmentDate, tz),
                paciente = a.Patient.Name,
                profissional = a.User.Name,
            }),
        });
    }

    private async Task<string> FreeSlotsAsync(JsonElement args)
    {
        var from = AgentTime.ParseDate(Str(args, "data_inicio"));
        var to = args.TryGetProperty("data_fim", out var end) && end.ValueKind == JsonValueKind.String
            ? AgentTime.ParseDate(end.GetString()!) : from;
        // ponytail: janela fixa de 7 dias e 07h–20h do GetDayScheduleAsync; ignora ProfessionalAvailability.
        if (to < from || to.DayNumber - from.DayNumber >= MaxSlotDays)
            to = from.AddDays(MaxSlotDays - 1);

        var professionalId = OptInt(args, "profissional_id");
        var professionals = (await appointments.GetProfessionalsAsync()).Value!
            .Where(p => professionalId == null || p.Id == professionalId).ToList();
        if (professionals.Count == 0)
            return Error("Profissional não encontrado.");

        var tz = await ClinicZoneAsync();
        var now = DateTimeOffset.UtcNow;
        var result = new List<object>();
        foreach (var professional in professionals)
        {
            var free = new List<string>();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                var schedule = await appointments.GetDayScheduleAsync(professional.Id, day);
                if (!schedule.IsSuccess) return Error(schedule.ErrorMessage!);
                free.AddRange(schedule.Value!.Slots.Where(s => s.Available && s.Start > now)
                    .Select(s => AgentTime.ToLocalIso(s.Start.UtcDateTime, tz)));
            }
            result.Add(new { profissional_id = professional.Id, nome = professional.Name, horarios_livres = free });
        }
        return Ok(new { ok = true, profissionais = result });
    }

    private async Task<string> ScheduleAsync(JsonElement args)
    {
        var tz = await ClinicZoneAsync();
        var result = await appointments.CreateAsync(new CreateAppointmentDto
        {
            PatientId = Int(args, "paciente_id"),
            ProfessionalId = Int(args, "profissional_id"),
            AppointmentDate = AgentTime.ToUtc(Str(args, "data_hora"), tz),
        });
        return result.IsSuccess
            ? Ok(new { ok = true, consulta_id = result.Value!.Id, paciente = result.Value.PatientName,
                profissional = result.Value.UserName, data_hora = AgentTime.ToLocalIso(result.Value.AppointmentDate, tz) })
            : Error(result.ErrorMessage!);
    }

    private async Task<string> RescheduleAsync(int appointmentId, string newLocal)
    {
        var tz = await ClinicZoneAsync();
        var result = await appointments.UpdateAsync(appointmentId, new UpdateAppointmentDto
        {
            AppointmentDate = AgentTime.ToUtc(newLocal, tz),
            Status = AppointmentStatus.Scheduled,
        });
        return result.IsSuccess
            ? Ok(new { ok = true, consulta_id = appointmentId, data_hora = AgentTime.ToLocalIso(result.Value!.AppointmentDate, tz) })
            : Error(result.ErrorMessage!);
    }

    private async Task<string> CancelAsync(int appointmentId)
    {
        var current = await appointments.GetByIdAsync(appointmentId);
        if (!current.IsSuccess) return Error(current.ErrorMessage!);
        if (current.Value!.Status != AppointmentStatus.Scheduled)
            return Error("Só é possível cancelar consulta agendada.");
        var result = await appointments.UpdateAsync(appointmentId, new UpdateAppointmentDto
        {
            AppointmentDate = current.Value!.AppointmentDate,
            Status = AppointmentStatus.Cancelled,
        });
        return result.IsSuccess ? Ok(new { ok = true, consulta_id = appointmentId }) : Error(result.ErrorMessage!);
    }

    private async Task<TimeZoneInfo> ClinicZoneAsync()
        => AgentTime.FindZone(await db.Clinicas.Where(c => c.Id == usuario.ClinicaId)
            .Select(c => c.TimeZoneId).FirstAsync());

    private static string Str(JsonElement args, string name)
        => args.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!
            : throw new ArgumentException($"Parâmetro obrigatório ausente: {name}.");

    private static int Int(JsonElement args, string name)
        => OptInt(args, name) ?? throw new ArgumentException($"Parâmetro obrigatório ausente: {name}.");

    private static int? OptInt(JsonElement args, string name)
    {
        if (!args.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)) return s;
        throw new ArgumentException($"Parâmetro inválido: {name}.");
    }

    private static JsonDocument? TryParse(string argumentsJson)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Ok(object value) => JsonSerializer.Serialize(value, Json);
    private static string Error(string message) => JsonSerializer.Serialize(new { ok = false, erro = message }, Json);
}
