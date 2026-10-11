using MultiClinica.API.Models;

namespace MultiClinica.API.Services.Agent;

public sealed class AgentOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://opencode.ai/zen/go/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? OwnerModel { get; set; }
    public string? PatientModel { get; set; }
    public int MaxToolIterations { get; set; } = 8;
    public int HistoryHours { get; set; } = 24;
    public int HistoryMaxMessages { get; set; } = 40;
    public int PollSeconds { get; set; } = 2;

    public string ModelFor(AgentProfile profile)
        => (profile == AgentProfile.Owner ? OwnerModel : PatientModel) is { Length: > 0 } specific ? specific : Model;
}
