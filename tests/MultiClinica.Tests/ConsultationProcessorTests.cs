using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MultiClinica.API.Data;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Agent;
using MultiClinica.API.Services.Transcription;
using Xunit;

namespace MultiClinica.Tests;

public class ConsultationProcessorTests
{
    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private static ConsultationProcessor CreateProcessor(MultiClinicaFactory app, ManualTimeProvider time,
        FakeTranscriber transcriber, FakeChatModel chat)
    {
        var services = app.Services;
        return new ConsultationProcessor(
            services.GetRequiredService<IServiceScopeFactory>(),
            transcriber,
            chat,
            services.GetRequiredService<ConsultationAudioStore>(),
            Options.Create(new AgentOptions { ApiKey = "test-key", Model = "test-model" }),
            time,
            NullLogger<ConsultationProcessor>.Instance);
    }

    private static async Task<ConsultationReport> SeedReportAsync(MultiClinicaFactory app,
        ConsultationTestSupport.SeededConsultation seeded, ConsultationStatus status, DateTime startedAt,
        DateTime? lastChunkAt = null, DateTime? nextAttemptAt = null, string? transcript = null,
        bool reportPending = false, int processingAttempts = 0)
    {
        ConsultationReport? report = null;
        await app.SeedAsync(async db =>
        {
            report = new ConsultationReport
            {
                ClinicaId = seeded.ClinicaId,
                AppointmentId = seeded.AppointmentId,
                PatientId = seeded.PatientId,
                ProfessionalId = seeded.ProfessionalId,
                RecordedByUserId = seeded.ProfessionalId,
                ConsentConfirmedAt = startedAt,
                Status = status,
                StartedAt = startedAt,
                EndedAt = status == ConsultationStatus.Recording ? null : startedAt,
                LastChunkAt = lastChunkAt,
                NextAttemptAt = nextAttemptAt,
                Transcript = transcript,
                ReportPending = reportPending,
                ProcessingAttempts = processingAttempts,
                ChunkCount = 2,
            };
            db.ConsultationReports.Add(report);
            await db.SaveChangesAsync();
        });
        return report!;
    }

    private static async Task AddChunksAsync(MultiClinicaFactory app, int reportId, params string[] chunks)
    {
        var store = app.Services.GetRequiredService<ConsultationAudioStore>();
        for (var index = 0; index < chunks.Length; index++)
        {
            await store.SaveChunkAsync(reportId, index,
                new MemoryStream(Encoding.UTF8.GetBytes(chunks[index])), default);
        }
    }

    private static async Task<ConsultationReport> ReadReportAsync(MultiClinicaFactory app, int reportId)
    {
        ConsultationReport? report = null;
        await app.SeedAsync(async db =>
            report = await db.ConsultationReports.AsNoTracking().SingleAsync(r => r.Id == reportId));
        return report!;
    }

    private static async Task<ClinicUsage> ReadUsageAsync(MultiClinicaFactory app, int clinicaId, UsageMetric metric)
    {
        ClinicUsage? usage = null;
        var period = UsageService.CurrentPeriod(DateTime.UtcNow);
        await app.SeedAsync(async db => usage = await db.ClinicUsages.AsNoTracking()
            .SingleAsync(u => u.ClinicaId == clinicaId && u.Period == period && u.Metric == metric));
        return usage!;
    }

    private const string SoapJson =
        """{"subjective":"Dor no joelho","objective":"Marcha normal","assessment":"Quadro leve","plan":"Repouso","suggestedReturnDays":7}""";

    [Fact]
    public async Task Interrompe_gravacao_sem_pedacos_ha_mais_de_10_minutos()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var transcriber = new FakeTranscriber();
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Recording,
            startedAt: time.Current.UtcDateTime.AddMinutes(-20),
            lastChunkAt: time.Current.UtcDateTime.AddMinutes(-11));

        await CreateProcessor(app, time, transcriber, new FakeChatModel()).ProcessBatchAsync(default);

        var updated = await ReadReportAsync(app, report.Id);
        Assert.Equal(ConsultationStatus.Interrupted, updated.Status);
        Assert.Empty(transcriber.ReceivedBytes);
    }

    [Fact]
    public async Task Transcreve_pedacos_apaga_audio_e_gera_relatorio_SOAP()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var transcriber = new FakeTranscriber().Reply("Paciente relata dor no joelho.", 30);
        var chat = new FakeChatModel().Reply(SoapJson);
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Processing,
            startedAt: time.Current.UtcDateTime.AddMinutes(-30),
            nextAttemptAt: time.Current.UtcDateTime.AddMinutes(-1));
        await AddChunksAsync(app, report.Id, "AAA", "BB");
        var store = app.Services.GetRequiredService<ConsultationAudioStore>();

        await CreateProcessor(app, time, transcriber, chat).ProcessBatchAsync(default);

        var updated = await ReadReportAsync(app, report.Id);
        Assert.Equal(ConsultationStatus.Ready, updated.Status);
        Assert.False(updated.ReportPending);
        Assert.Null(updated.NextAttemptAt);
        Assert.Equal("Paciente relata dor no joelho.", updated.Transcript);
        Assert.Equal(30, updated.DurationSeconds);
        Assert.Equal(1, updated.MinutesCharged);
        Assert.Equal("Dor no joelho", updated.Subjective);
        Assert.Equal("Marcha normal", updated.Objective);
        Assert.Equal("Quadro leve", updated.Assessment);
        Assert.Equal("Repouso", updated.Plan);
        Assert.Equal(7, updated.SuggestedReturnDays);
        Assert.Equal(new long[] { 5 }, transcriber.ReceivedBytes);

        Assert.Empty(store.ListChunks(report.Id));
        Assert.False(Directory.Exists(store.DirectoryFor(report.Id)));

        var request = Assert.Single(chat.Requests);
        Assert.Equal("test-model", request.Model);
        Assert.Equal(2, request.Messages.Count);
        Assert.Equal(AgentChatRole.System, request.Messages[0].Role);
        Assert.Equal(AgentChatRole.User, request.Messages[1].Role);
        Assert.Equal("Paciente relata dor no joelho.", request.Messages[1].Content);
    }

    [Fact]
    public async Task Consome_minutos_arredondados_para_cima_mesmo_passando_da_cota()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        await app.SeedAsync(async db =>
        {
            db.ClinicUsages.Add(new ClinicUsage
            {
                ClinicaId = seeded.ClinicaId,
                Period = UsageService.CurrentPeriod(DateTime.UtcNow),
                Metric = UsageMetric.TranscriptionMinutes,
                Used = 1200,
            });
            await db.SaveChangesAsync();
        });
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var transcriber = new FakeTranscriber().Reply("Transcrição longa.", 61);
        var chat = new FakeChatModel().Reply(SoapJson);
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Processing,
            startedAt: time.Current.UtcDateTime.AddMinutes(-30),
            nextAttemptAt: time.Current.UtcDateTime.AddMinutes(-1));
        await AddChunksAsync(app, report.Id, "AAA");

        await CreateProcessor(app, time, transcriber, chat).ProcessBatchAsync(default);

        var updated = await ReadReportAsync(app, report.Id);
        Assert.Equal(2, updated.MinutesCharged);
        var usage = await ReadUsageAsync(app, seeded.ClinicaId, UsageMetric.TranscriptionMinutes);
        Assert.Equal(1202, usage.Used);
    }

    [Fact]
    public async Task Relatorio_aceita_JSON_dentro_de_bloco_markdown()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var chat = new FakeChatModel().Reply("""
            ```json
            {"subjective":"S","objective":"O","assessment":"A","plan":"P","suggestedReturnDays":null}
            ```
            """);
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Processing,
            startedAt: time.Current.UtcDateTime.AddMinutes(-30),
            nextAttemptAt: time.Current.UtcDateTime.AddMinutes(-1), transcript: "Transcrição já salva.");

        await CreateProcessor(app, time, new FakeTranscriber(), chat).ProcessBatchAsync(default);

        var updated = await ReadReportAsync(app, report.Id);
        Assert.Equal(ConsultationStatus.Ready, updated.Status);
        Assert.Equal("S", updated.Subjective);
        Assert.Equal("O", updated.Objective);
        Assert.Equal("A", updated.Assessment);
        Assert.Equal("P", updated.Plan);
        Assert.Null(updated.SuggestedReturnDays);
    }

    [Fact]
    public async Task Falha_da_IA_deixa_relatorio_pendente_sem_transcrever_de_novo()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var transcriber = new FakeTranscriber().Reply("Transcrição guardada.", 45);
        var chat = new FakeChatModel().Throw();
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Processing,
            startedAt: time.Current.UtcDateTime.AddMinutes(-30),
            nextAttemptAt: time.Current.UtcDateTime.AddMinutes(-1));
        await AddChunksAsync(app, report.Id, "AAA");
        var processor = CreateProcessor(app, time, transcriber, chat);

        await processor.ProcessBatchAsync(default);

        var pending = await ReadReportAsync(app, report.Id);
        Assert.Equal(ConsultationStatus.Ready, pending.Status);
        Assert.True(pending.ReportPending);
        Assert.Equal("Transcrição guardada.", pending.Transcript);
        Assert.Equal(time.Current.UtcDateTime.AddMinutes(30), pending.NextAttemptAt);
        Assert.Single(transcriber.ReceivedBytes);

        time.Current = time.Current.AddMinutes(31);
        chat.Reply(SoapJson);
        await processor.ProcessBatchAsync(default);

        var ready = await ReadReportAsync(app, report.Id);
        Assert.Equal(ConsultationStatus.Ready, ready.Status);
        Assert.False(ready.ReportPending);
        Assert.Null(ready.NextAttemptAt);
        Assert.Equal("Dor no joelho", ready.Subjective);
        Assert.Single(transcriber.ReceivedBytes);
    }

    [Fact]
    public async Task Transcricao_vazia_nao_transcreve_nem_cobra_de_novo()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var transcriber = new FakeTranscriber().Reply("", 30);
        var chat = new FakeChatModel().Throw().Reply(SoapJson);
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Processing,
            startedAt: time.Current.UtcDateTime.AddMinutes(-30),
            nextAttemptAt: time.Current.UtcDateTime.AddMinutes(-1));
        await AddChunksAsync(app, report.Id, "AAA");
        var processor = CreateProcessor(app, time, transcriber, chat);

        await processor.ProcessBatchAsync(default);
        time.Current = time.Current.AddMinutes(31);
        await processor.ProcessBatchAsync(default);

        var ready = await ReadReportAsync(app, report.Id);
        Assert.False(ready.ReportPending);
        Assert.Single(transcriber.ReceivedBytes);
        Assert.Equal(1, (await ReadUsageAsync(app, seeded.ClinicaId, UsageMetric.TranscriptionMinutes)).Used);
    }

    [Fact]
    public async Task Transcritor_falhando_tres_vezes_marca_Failed()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var transcriber = new FakeTranscriber().Throw().Throw().Throw();
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Processing,
            startedAt: time.Current.UtcDateTime.AddMinutes(-30),
            nextAttemptAt: time.Current.UtcDateTime.AddMinutes(-1));
        await AddChunksAsync(app, report.Id, "AAA");
        var processor = CreateProcessor(app, time, transcriber, new FakeChatModel());
        var store = app.Services.GetRequiredService<ConsultationAudioStore>();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await processor.ProcessBatchAsync(default);
            var updated = await ReadReportAsync(app, report.Id);
            Assert.Equal(attempt, updated.ProcessingAttempts);
            if (attempt < 3)
            {
                Assert.Equal(ConsultationStatus.Processing, updated.Status);
                Assert.NotNull(updated.NextAttemptAt);
                time.Current = time.Current.AddMinutes(attempt == 1 ? 2 : 6);
                continue;
            }

            Assert.Equal(ConsultationStatus.Failed, updated.Status);
            Assert.Equal("Transcrição indisponível", updated.FailureReason);
            Assert.Null(updated.NextAttemptAt);
            Assert.Null(updated.Transcript);
        }

        // O áudio só é apagado pela retenção de 7 dias.
        Assert.NotEmpty(store.ListChunks(report.Id));
    }

    [Fact]
    public async Task Transcritor_nao_configurado_marca_Failed_com_motivo_especifico()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var transcriber = new FakeTranscriber().NotConfigured();
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Processing,
            startedAt: time.Current.UtcDateTime.AddMinutes(-30),
            nextAttemptAt: time.Current.UtcDateTime.AddMinutes(-1), processingAttempts: 2);
        await AddChunksAsync(app, report.Id, "AAA");

        await CreateProcessor(app, time, transcriber, new FakeChatModel()).ProcessBatchAsync(default);

        var updated = await ReadReportAsync(app, report.Id);
        Assert.Equal(ConsultationStatus.Failed, updated.Status);
        Assert.Equal("Transcrição não configurada", updated.FailureReason);
        Assert.Equal(3, updated.ProcessingAttempts);
    }

    [Fact]
    public async Task Limpa_audio_de_relatorio_falho_apos_sete_dias()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var report = await SeedReportAsync(app, seeded, ConsultationStatus.Failed,
            startedAt: time.Current.UtcDateTime.AddDays(-8), lastChunkAt: time.Current.UtcDateTime.AddDays(-8));
        await AddChunksAsync(app, report.Id, "AAA");
        var store = app.Services.GetRequiredService<ConsultationAudioStore>();
        Assert.NotEmpty(store.ListChunks(report.Id));

        time.Current = time.Current.AddDays(8);
        await CreateProcessor(app, time, new FakeTranscriber(), new FakeChatModel()).ProcessBatchAsync(default);

        Assert.Empty(store.ListChunks(report.Id));
        Assert.False(Directory.Exists(store.DirectoryFor(report.Id)));
    }
}
