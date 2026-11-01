using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Common;
using MultiClinica.API.Models;
using MultiClinica.API.Services;
using MultiClinica.API.Services.Transcription;
using Xunit;

namespace MultiClinica.Tests;

public class ConsultationEndpointsTests
{
    private const int MaxChunkBytes = 2 * 1024 * 1024;

    // ── Início ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_without_consent_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation",
            new { consentConfirmed = false });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ConsentRequired);
    }

    [Fact]
    public async Task Start_cancelled_appointment_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        await app.SeedAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync(a => a.Id == seed.AppointmentId);
            appointment.Status = AppointmentStatus.Cancelled;
            await db.SaveChangesAsync();
        });
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation",
            new { consentConfirmed = true });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.AppointmentCancelled);
    }

    [Fact]
    public async Task Start_duplicated_report_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation",
            new { consentConfirmed = true });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ConsultationExists);
    }

    [Fact]
    public async Task Start_with_exhausted_quota_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        await app.SeedAsync(async db =>
        {
            db.ClinicUsages.Add(new ClinicUsage
            {
                ClinicaId = seed.ClinicaId,
                Period = UsageService.CurrentPeriod(DateTime.UtcNow),
                Metric = UsageMetric.TranscriptionMinutes,
                Used = PlanMatrix.For(ClinicPlan.Premium).TranscriptionMinutesPerMonth,
            });
            await db.SaveChangesAsync();
        });
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation",
            new { consentConfirmed = true });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.TranscriptionQuotaExhausted);
    }

    [Fact]
    public async Task Start_from_another_professional_is_forbidden()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.OtherProfessionalEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation",
            new { consentConfirmed = true });

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Start_from_reception_is_forbidden()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ReceptionEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation",
            new { consentConfirmed = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Start_on_free_plan_requires_the_transcription_feature()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app, ClinicPlan.Free);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation",
            new { consentConfirmed = true });

        await AssertErrorAsync(response, HttpStatusCode.PaymentRequired, ErrorCodes.PlanRequired);
    }

    [Fact]
    public async Task Start_creates_recording_report_and_marks_appointment_completed()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);

        var reportId = await StartAsync(client, seed.AppointmentId);

        await app.SeedAsync(async db =>
        {
            var appointment = await db.Appointments.SingleAsync(a => a.Id == seed.AppointmentId);
            Assert.Equal(AppointmentStatus.Completed, appointment.Status);

            var report = await db.ConsultationReports.SingleAsync(r => r.Id == reportId);
            Assert.Equal(seed.ClinicaId, report.ClinicaId);
            Assert.Equal(seed.PatientId, report.PatientId);
            Assert.Equal(seed.ProfessionalId, report.ProfessionalId);
            Assert.Equal(seed.ProfessionalId, report.RecordedByUserId);
            Assert.Equal(ConsultationStatus.Recording, report.Status);
            Assert.Equal(report.StartedAt, report.ConsentConfirmedAt);
        });
    }

    // ── Pedaços ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Chunk_same_number_is_idempotent_and_keeps_chunk_count()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        var reportId = await StartAsync(client, seed.AppointmentId);

        Assert.Equal(HttpStatusCode.NoContent,
            (await PutChunkAsync(client, seed.AppointmentId, 0, "primeiro")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await PutChunkAsync(client, seed.AppointmentId, 1, "segundo")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await PutChunkAsync(client, seed.AppointmentId, 0, "corrigido")).StatusCode);

        var store = app.Services.GetRequiredService<ConsultationAudioStore>();
        var chunks = store.ListChunks(reportId);
        Assert.Equal(2, chunks.Count);
        Assert.Equal("corrigido", await File.ReadAllTextAsync(chunks[0]));

        await app.SeedAsync(async db =>
        {
            var report = await db.ConsultationReports.SingleAsync(r => r.Id == reportId);
            Assert.Equal(2, report.ChunkCount);
            Assert.NotNull(report.LastChunkAt);
            Assert.Equal(ConsultationStatus.Recording, report.Status);
        });
    }

    [Fact]
    public async Task Chunk_above_two_megabytes_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        var reportId = await StartAsync(client, seed.AppointmentId);

        var response = await PutChunkAsync(client, seed.AppointmentId, 0, new byte[MaxChunkBytes + 1]);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.FileTooLarge);
        var store = app.Services.GetRequiredService<ConsultationAudioStore>();
        Assert.Empty(store.ListChunks(reportId));
    }

    [Fact]
    public async Task Chunk_while_processing_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await SetReportAsync(app, seed.AppointmentId, report => report.Status = ConsultationStatus.Processing);

        var response = await PutChunkAsync(client, seed.AppointmentId, 0);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.InvalidStatus);
    }

    [Fact]
    public async Task Chunk_resumes_interrupted_report()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        var reportId = await StartAsync(client, seed.AppointmentId);
        await PutChunkAsync(client, seed.AppointmentId, 0);
        await SetReportAsync(app, seed.AppointmentId, report => report.Status = ConsultationStatus.Interrupted);

        var response = await PutChunkAsync(client, seed.AppointmentId, 1);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var report = await ReportAsync(app, seed.AppointmentId);
        Assert.Equal(reportId, report.Id);
        Assert.Equal(ConsultationStatus.Recording, report.Status);
        Assert.Equal(2, report.ChunkCount);
    }

    // ── Encerramento e cobrança ──────────────────────────────────────────────

    [Fact]
    public async Task Finish_without_chunks_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);

        var response = await FinishAsync(client, seed.AppointmentId);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ConsultationEmpty);
    }

    [Fact]
    public async Task Finish_with_charge_creates_pending_payment_for_the_appointment_month()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.AdminEmail);
        await StartAsync(client, seed.AppointmentId);
        await PutChunkAsync(client, seed.AppointmentId, 0);

        var response = await FinishAsync(client, seed.AppointmentId,
            new { planId = seed.PlanId, paymentMethod = PaymentMethods.Pix, sendOnline = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Processing", body.GetProperty("status").GetString());
        Assert.Null(body.GetProperty("chargeError").GetString());
        var paymentId = body.GetProperty("paymentId").GetInt32();

        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(p => p.Id == paymentId);
            Assert.Equal(seed.PatientId, payment.PatientId);
            Assert.Equal(seed.ProfessionalId, payment.UserId);
            Assert.Equal(seed.PlanId, payment.PlanId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
            Assert.Equal(CurrentMonthInSaoPaulo(), payment.ReferenceMonth);

            var report = await db.ConsultationReports.SingleAsync(r => r.AppointmentId == seed.AppointmentId);
            Assert.Equal(ConsultationStatus.Processing, report.Status);
            Assert.NotNull(report.EndedAt);
            Assert.NotNull(report.NextAttemptAt);
            Assert.Equal(0, report.ProcessingAttempts);
        });
    }

    [Fact]
    public async Task Finish_duplicate_monthly_charge_keeps_the_consultation_processing()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        await app.SeedAsync(async db =>
        {
            db.Payments.Add(new Payment
            {
                ClinicaId = seed.ClinicaId,
                PatientId = seed.PatientId,
                UserId = seed.ProfessionalId,
                PlanId = seed.PlanId,
                Amount = 150m,
                ReferenceMonth = CurrentMonthInSaoPaulo(),
                PaymentMethod = PaymentMethods.Pix,
                Status = PaymentStatus.Cancelled,
            });
            await db.SaveChangesAsync();
        });
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await PutChunkAsync(client, seed.AppointmentId, 0);

        var response = await FinishAsync(client, seed.AppointmentId,
            new { planId = seed.PlanId, paymentMethod = PaymentMethods.Pix, sendOnline = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Processing", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("paymentId").ValueKind == JsonValueKind.Null);
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("chargeError").GetString()));

        var report = await ReportAsync(app, seed.AppointmentId);
        Assert.Equal(ConsultationStatus.Processing, report.Status);
    }

    [Fact]
    public async Task Professional_can_charge_when_finishing()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await PutChunkAsync(client, seed.AppointmentId, 0);

        var response = await FinishAsync(client, seed.AppointmentId,
            new { planId = seed.PlanId, paymentMethod = PaymentMethods.Pix, sendOnline = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var paymentId = body.GetProperty("paymentId").GetInt32();
        await app.SeedAsync(async db =>
        {
            var payment = await db.Payments.SingleAsync(p => p.Id == paymentId);
            Assert.Equal(seed.ProfessionalId, payment.UserId);
            Assert.Equal(PaymentStatus.Pending, payment.Status);
        });
    }

    // ── Retry, edição e revisão ──────────────────────────────────────────────

    [Fact]
    public async Task Retry_failed_without_chunks_is_rejected()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await SetReportAsync(app, seed.AppointmentId, report => report.Status = ConsultationStatus.Failed);

        var response = await client.PostAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/retry", null);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ConsultationEmpty);
    }

    [Fact]
    public async Task Retry_failed_with_chunks_returns_to_processing()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await PutChunkAsync(client, seed.AppointmentId, 0);
        await SetReportAsync(app, seed.AppointmentId, report =>
        {
            report.Status = ConsultationStatus.Failed;
            report.FailureReason = "Transcrição indisponível";
            report.ProcessingAttempts = 3;
        });

        var response = await client.PostAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/retry", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Processing", body.GetProperty("status").GetString());
        var report = await ReportAsync(app, seed.AppointmentId);
        Assert.Equal(ConsultationStatus.Processing, report.Status);
        Assert.Equal(0, report.ProcessingAttempts);
        Assert.Null(report.FailureReason);
        Assert.NotNull(report.NextAttemptAt);
    }

    [Fact]
    public async Task Retry_ready_pending_report_only_schedules_a_new_attempt()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await SetReportAsync(app, seed.AppointmentId, report =>
        {
            report.Status = ConsultationStatus.Ready;
            report.ReportPending = true;
        });

        var response = await client.PostAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/retry", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Ready", body.GetProperty("status").GetString());
        var report = await ReportAsync(app, seed.AppointmentId);
        Assert.True(report.ReportPending);
        Assert.NotNull(report.NextAttemptAt);
    }

    [Fact]
    public async Task Report_edit_and_review_require_ready_status()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await SetReportAsync(app, seed.AppointmentId, report => report.Status = ConsultationStatus.Processing);

        var update = await client.PutAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/report",
            new { subjective = "Subjetivo", objective = "Objetivo", assessment = "Avaliação", plan = "Plano" });
        await AssertErrorAsync(update, HttpStatusCode.BadRequest, ErrorCodes.InvalidStatus);

        var review = await client.PostAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/review", null);
        await AssertErrorAsync(review, HttpStatusCode.BadRequest, ErrorCodes.InvalidStatus);
    }

    [Fact]
    public async Task Report_edit_trims_fields_and_review_marks_reviewed()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await SetReportAsync(app, seed.AppointmentId, report => report.Status = ConsultationStatus.Ready);

        var update = await client.PutAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/report",
            new { subjective = "  Dor no joelho  ", objective = "  ", assessment = "Avaliação", plan = "Plano" });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadJsonAsync(update);
        Assert.Equal("Dor no joelho", updated.GetProperty("subjective").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("objective").ValueKind);
        Assert.Equal("Avaliação", updated.GetProperty("assessment").GetString());

        var review = await client.PostAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/review", null);

        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var reviewed = await ReadJsonAsync(review);
        Assert.False(string.IsNullOrWhiteSpace(reviewed.GetProperty("reviewedAt").GetString()));
        Assert.Equal(seed.ProfessionalId, reviewed.GetProperty("reviewedByUserId").GetInt32());
    }

    [Fact]
    public async Task Appointment_list_and_detail_expose_consultation_status_and_review()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);

        using var before = JsonDocument.Parse(
            await client.GetStringAsync($"/api/appointments/{seed.AppointmentId}"));
        Assert.Equal("Recording", before.RootElement.GetProperty("consultationStatus").GetString());
        Assert.False(before.RootElement.GetProperty("consultationReviewed").GetBoolean());

        await SetReportAsync(app, seed.AppointmentId, report => report.Status = ConsultationStatus.Ready);
        (await client.PostAsync($"/api/appointments/{seed.AppointmentId}/consultation/review", null))
            .EnsureSuccessStatusCode();

        using var detail = JsonDocument.Parse(
            await client.GetStringAsync($"/api/appointments/{seed.AppointmentId}"));
        Assert.Equal("Ready", detail.RootElement.GetProperty("consultationStatus").GetString());
        Assert.True(detail.RootElement.GetProperty("consultationReviewed").GetBoolean());

        using var list = JsonDocument.Parse(
            await client.GetStringAsync("/api/appointments?page=1&pageSize=50"));
        var item = list.RootElement.GetProperty("data").EnumerateArray()
            .Single(a => a.GetProperty("id").GetInt32() == seed.AppointmentId);
        Assert.Equal("Ready", item.GetProperty("consultationStatus").GetString());
        Assert.True(item.GetProperty("consultationReviewed").GetBoolean());
    }

    // ── Sugestão de cobrança, evolução e prontuário ─────────────────────────

    [Fact]
    public async Task Charge_suggestion_uses_last_plan_and_flags_monthly_charge()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        var monthlyPlanId = 0;
        await app.SeedAsync(async db =>
        {
            var sessionTypeId = await db.Plans.Where(p => p.Id == seed.PlanId)
                .Select(p => p.TipoSessaoId).SingleAsync();
            var monthly = new Plans
            {
                ClinicaId = seed.ClinicaId,
                Name = "Acompanhamento mensal",
                Valor = 300m,
                TipoPlano = TipoPlano.Mensal,
                TipoSessaoId = sessionTypeId,
            };
            db.Plans.Add(monthly);
            await db.SaveChangesAsync();
            monthlyPlanId = monthly.Id;

            var oldPayment = new Payment
            {
                ClinicaId = seed.ClinicaId,
                PatientId = seed.PatientId,
                UserId = seed.ProfessionalId,
                PlanId = seed.PlanId,
                Amount = 150m,
                ReferenceMonth = CurrentMonthInSaoPaulo().AddMonths(-1),
                PaymentMethod = PaymentMethods.Pix,
                Status = PaymentStatus.Paid,
            };
            db.Payments.Add(oldPayment);
            await db.SaveChangesAsync();
            oldPayment.CreatedAt = DateTime.UtcNow.AddDays(-10);
            await db.SaveChangesAsync();

            db.Payments.Add(new Payment
            {
                ClinicaId = seed.ClinicaId,
                PatientId = seed.PatientId,
                UserId = seed.ProfessionalId,
                PlanId = monthlyPlanId,
                Amount = 300m,
                ReferenceMonth = CurrentMonthInSaoPaulo(),
                PaymentMethod = PaymentMethods.Pix,
                Status = PaymentStatus.Pending,
            });
            await db.SaveChangesAsync();
        });
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.GetAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/charge-suggestion");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(monthlyPlanId, body.GetProperty("planId").GetInt32());
        Assert.Equal("Acompanhamento mensal", body.GetProperty("planName").GetString());
        Assert.Equal("Mensal", body.GetProperty("planType").GetString());
        Assert.True(body.GetProperty("alreadyChargedThisMonth").GetBoolean());
    }

    [Fact]
    public async Task Charge_suggestion_without_history_returns_empty_and_not_charged()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);

        var response = await client.GetAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/charge-suggestion");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("planId").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("planName").ValueKind);
        Assert.False(body.GetProperty("alreadyChargedThisMonth").GetBoolean());
        // Profissional não acessa api/plans: os planos ativos vêm na sugestão.
        var plans = body.GetProperty("plans").EnumerateArray().ToList();
        Assert.Single(plans);
        Assert.Equal(seed.PlanId, plans[0].GetProperty("id").GetInt32());
        Assert.Equal("Avulso", plans[0].GetProperty("planType").GetString());
        Assert.False(body.GetProperty("onlineAvailable").GetBoolean()); // sem Mercado Pago conectado
    }

    [Fact]
    public async Task Evolution_from_ready_report_maps_soap_fields()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await SetReportAsync(app, seed.AppointmentId, report =>
        {
            report.Status = ConsultationStatus.Ready;
            report.Subjective = "Relata dor";
            report.Objective = "Edema leve";
            report.Assessment = "Quadro em melhora";
            report.Plan = "Manter exercícios";
        });

        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{seed.AppointmentId}/consultation/evolution", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var evolutionId = body.GetProperty("evolutionId").GetInt32();
        await app.SeedAsync(async db =>
        {
            var evolution = await db.PatientEvolutions.SingleAsync(e => e.Id == evolutionId);
            Assert.Equal(seed.PatientId, evolution.PatientId);
            Assert.Equal(seed.AppointmentId, evolution.AppointmentId);
            Assert.Equal(seed.ProfessionalId, evolution.ProfessionalId);
            Assert.Equal(EvolutionStatus.Draft, evolution.Status);
            Assert.Equal("Relata dor\n\nEdema leve", evolution.Description);
            Assert.Equal("Quadro em melhora", evolution.Observations);
            Assert.Equal("Manter exercícios", evolution.Conduct);
        });
    }

    [Fact]
    public async Task Consultation_can_have_several_evolutions_listed_in_the_report()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(client, seed.AppointmentId);
        await SetReportAsync(app, seed.AppointmentId, report => report.Status = ConsultationStatus.Ready);

        var url = $"/api/appointments/{seed.AppointmentId}/consultation/evolution";
        var first = (await ReadJsonAsync(await client.PostAsJsonAsync(url, new { }))).GetProperty("evolutionId").GetInt32();
        var second = (await ReadJsonAsync(await client.PostAsJsonAsync(url, new { }))).GetProperty("evolutionId").GetInt32();

        var report = await ReadJsonAsync(await client.GetAsync($"/api/appointments/{seed.AppointmentId}/consultation"));
        var ids = report.GetProperty("evolutionIds").EnumerateArray().Select(e => e.GetInt32()).ToList();
        Assert.Equal([first, second], ids);
    }

    [Fact]
    public async Task Patient_report_list_filters_professional_and_shows_all_for_admin()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seed = await ConsultationTestSupport.SeedAsync(app);
        var otherAppointmentId = await SeedAppointmentAsync(
            app, seed.ClinicaId, seed.OtherProfessionalId, seed.PatientId, DateTime.UtcNow.AddDays(1));
        await app.SeedAsync(async db =>
        {
            db.ConsultationReports.Add(new ConsultationReport
            {
                ClinicaId = seed.ClinicaId,
                AppointmentId = otherAppointmentId,
                PatientId = seed.PatientId,
                ProfessionalId = seed.OtherProfessionalId,
                RecordedByUserId = seed.OtherProfessionalId,
                ConsentConfirmedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow,
                Status = ConsultationStatus.Ready,
                ReviewedAt = DateTime.UtcNow,
                ReviewedByUserId = seed.AdminId,
            });
            await db.SaveChangesAsync();
        });

        using var professional = await ConsultationTestSupport.LoginAsync(app, seed.ProfessionalEmail);
        await StartAsync(professional, seed.AppointmentId);

        var own = await professional.GetAsync($"/api/patients/{seed.PatientId}/consultation-reports");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var ownBody = await ReadJsonAsync(own);
        Assert.Equal(1, ownBody.GetArrayLength());
        Assert.Equal(seed.AppointmentId, ownBody[0].GetProperty("appointmentId").GetInt32());
        Assert.Equal("Dra. Ana", ownBody[0].GetProperty("professionalName").GetString());
        Assert.False(ownBody[0].GetProperty("reviewed").GetBoolean());

        using var other = await ConsultationTestSupport.LoginAsync(app, seed.OtherProfessionalEmail);
        var otherOnly = await ReadJsonAsync(
            await other.GetAsync($"/api/patients/{seed.PatientId}/consultation-reports"));
        Assert.Equal(1, otherOnly.GetArrayLength());
        Assert.Equal(otherAppointmentId, otherOnly[0].GetProperty("appointmentId").GetInt32());
        Assert.True(otherOnly[0].GetProperty("reviewed").GetBoolean());

        using var admin = await ConsultationTestSupport.LoginAsync(app, seed.AdminEmail);
        var all = await ReadJsonAsync(
            await admin.GetAsync($"/api/patients/{seed.PatientId}/consultation-reports"));
        Assert.Equal(2, all.GetArrayLength());
        Assert.Equal(otherAppointmentId, all[0].GetProperty("appointmentId").GetInt32());
        Assert.Equal(seed.AppointmentId, all[1].GetProperty("appointmentId").GetInt32());
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private static async Task<int> StartAsync(HttpClient client, int appointmentId, bool consent = true)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/appointments/{appointmentId}/consultation",
            new { consentConfirmed = consent });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        return body.GetProperty("reportId").GetInt32();
    }

    private static Task<HttpResponseMessage> PutChunkAsync(
        HttpClient client, int appointmentId, int n, string? text = null)
        => PutChunkAsync(client, appointmentId, n, Encoding.UTF8.GetBytes(text ?? "audio"));

    private static async Task<HttpResponseMessage> PutChunkAsync(
        HttpClient client, int appointmentId, int n, byte[] bytes)
    {
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/webm");
        return await client.PutAsync(
            $"/api/appointments/{appointmentId}/consultation/chunks/{n}", content);
    }

    private static Task<HttpResponseMessage> FinishAsync(
        HttpClient client, int appointmentId, object? charge = null)
        => client.PostAsJsonAsync(
            $"/api/appointments/{appointmentId}/consultation/finish", new { charge });

    private static Task SetReportAsync(
        MultiClinicaFactory app, int appointmentId, Action<ConsultationReport> change)
        => app.SeedAsync(async db =>
        {
            var report = await db.ConsultationReports.SingleAsync(r => r.AppointmentId == appointmentId);
            change(report);
            await db.SaveChangesAsync();
        });

    private static async Task<ConsultationReport> ReportAsync(
        MultiClinicaFactory app, int appointmentId)
    {
        ConsultationReport? report = null;
        await app.SeedAsync(async db => report = await db.ConsultationReports
            .AsNoTracking()
            .SingleAsync(r => r.AppointmentId == appointmentId));
        return report!;
    }

    private static async Task<int> SeedAppointmentAsync(
        MultiClinicaFactory app, int clinicaId, int professionalId, int patientId, DateTime date)
    {
        var appointmentId = 0;
        await app.SeedAsync(async db =>
        {
            var appointment = new Appointment
            {
                ClinicaId = clinicaId,
                UserId = professionalId,
                PatientId = patientId,
                AppointmentDate = date,
                Status = AppointmentStatus.Scheduled,
            };
            db.Appointments.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        });
        return appointmentId;
    }

    private static DateOnly CurrentMonthInSaoPaulo()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        return new DateOnly(local.Year, local.Month, 1);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
