using MultiClinica.API.Common;
using MultiClinica.API.Models;
using Xunit;

namespace MultiClinica.Tests;

public class PlanMatrixTests_Plans
{
    [Fact]
    public void Precos_e_anual_seguem_o_spec()
    {
        Assert.Equal(0m, PlanMatrix.For(ClinicPlan.Free).MonthlyPrice);
        Assert.Equal(49.90m, PlanMatrix.For(ClinicPlan.Pro).MonthlyPrice);
        Assert.Equal(79.90m, PlanMatrix.For(ClinicPlan.Premium).MonthlyPrice);
        Assert.Equal(499.90m, PlanMatrix.AnnualPrice(ClinicPlan.Pro));
        Assert.Equal(799.90m, PlanMatrix.AnnualPrice(ClinicPlan.Premium));
    }

    [Theory]
    [InlineData(ClinicPlan.Free, Features.OwnerCopilot, false)]
    [InlineData(ClinicPlan.Pro, Features.OwnerCopilot, true)]
    [InlineData(ClinicPlan.Pro, Features.OnlinePayments, true)]
    [InlineData(ClinicPlan.Pro, Features.Transcription, false)]
    [InlineData(ClinicPlan.Premium, Features.Transcription, true)]
    [InlineData(ClinicPlan.Premium, Features.PatientChatbot, true)]
    [InlineData(ClinicPlan.Premium, Features.WhatsAppReminders, true)]
    public void Features_por_plano(ClinicPlan plan, string feature, bool expected)
        => Assert.Equal(expected, PlanMatrix.Has(plan, feature));

    [Theory]
    [InlineData(ClinicPlan.Free, 0, 1)]
    [InlineData(ClinicPlan.Free, 1, 2)]
    [InlineData(ClinicPlan.Free, 2, 2)]   // vaga comprada acima do máximo do plano não conta
    [InlineData(ClinicPlan.Pro, 0, 3)]
    [InlineData(ClinicPlan.Pro, 2, 5)]
    [InlineData(ClinicPlan.Pro, 5, 5)]
    public void Limite_efetivo_de_profissionais(ClinicPlan plan, int extras, int expected)
        => Assert.Equal(expected, PlanMatrix.ProfessionalLimit(plan, extras));

    [Fact]
    public void Premium_e_ilimitado()
        => Assert.Null(PlanMatrix.ProfessionalLimit(ClinicPlan.Premium, 0));

    [Fact]
    public void Cotas_mensais()
    {
        Assert.Equal(0, PlanMatrix.MonthlyQuota(ClinicPlan.Free, UsageMetric.WhatsAppConversations));
        Assert.Equal(300, PlanMatrix.MonthlyQuota(ClinicPlan.Pro, UsageMetric.WhatsAppConversations));
        Assert.Equal(0, PlanMatrix.MonthlyQuota(ClinicPlan.Pro, UsageMetric.TranscriptionMinutes));
        Assert.Equal(800, PlanMatrix.MonthlyQuota(ClinicPlan.Premium, UsageMetric.WhatsAppConversations));
        Assert.Equal(1200, PlanMatrix.MonthlyQuota(ClinicPlan.Premium, UsageMetric.TranscriptionMinutes));
    }

    [Fact]
    public void Plano_mais_barato_que_libera_a_feature()
    {
        Assert.Equal(ClinicPlan.Pro, PlanMatrix.CheapestWith(Features.OwnerCopilot));
        Assert.Equal(ClinicPlan.Premium, PlanMatrix.CheapestWith(Features.Transcription));
    }

    [Fact]
    public void Pacotes_do_spec()
    {
        var ids = PlanMatrix.Packs.Select(p => p.Id).ToArray();
        Assert.Equal(["pro-conversations-200", "premium-conversations-500", "premium-transcription-300"], ids);
        Assert.Equal(59.90m, PlanMatrix.ExtraProfessionalPrice);
        // Mesmos valores cadastrados no AbacatePay: se mudar lá, mude aqui (o app mostra estes preços).
        Assert.Equal([29.90m, 59.90m, 29.90m], PlanMatrix.Packs.Select(p => p.Price));
    }
}
