using MultiClinica.API.DTOs.Evolution;

namespace MultiClinica.API.Services.Interfaces;

public interface IEvolutionDashboardService
{
    Task<EvolutionDashboardSummaryDto> GetSummaryAsync();
}
