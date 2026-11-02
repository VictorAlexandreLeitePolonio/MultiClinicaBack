using Xunit;
using Microsoft.Extensions.DependencyInjection;
using MultiClinica.API.Services.Transcription;

namespace MultiClinica.Tests;

public class ConsultationFoundationTests
{
    [Fact]
    public async Task AudioStore_ConcatenatesChunksInOrder_AndDeletes()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var store = app.Services.GetRequiredService<ConsultationAudioStore>();

        await store.SaveChunkAsync(1, 1, new MemoryStream("B"u8.ToArray()), default);
        await store.SaveChunkAsync(1, 0, new MemoryStream("A"u8.ToArray()), default);
        await store.SaveChunkAsync(1, 1, new MemoryStream("C"u8.ToArray()), default); // sobrescreve

        var temp = await store.ConcatToTempFileAsync(1, default);
        try { Assert.Equal("AC", await File.ReadAllTextAsync(temp)); }
        finally { File.Delete(temp); }

        store.Delete(1);
        Assert.Empty(store.ListChunks(1));
        store.Delete(1); // pasta inexistente não lança
    }

    [Fact]
    public async Task Seed_ProfessionalCanLogIn()
    {
        await using var app = ConsultationTestSupport.CreateApp();
        var seeded = await ConsultationTestSupport.SeedAsync(app);
        using var client = await ConsultationTestSupport.LoginAsync(app, seeded.ProfessionalEmail);
        var response = await client.GetAsync($"/api/appointments/{seeded.AppointmentId}");
        response.EnsureSuccessStatusCode();
    }
}
