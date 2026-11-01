using MultiClinica.API.Data;
using MultiClinica.API.DTOs.Evolution;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Services;

// Stub da Fundação: a Trilha B reescreve por séries paciente + ficha (sem acompanhamento).
// Versão anterior para referência: git show 1c7d272:Services/EvolutionDashboardService.cs
public class EvolutionDashboardService(AppDbContext db, IUsuarioLogadoService usuario) : IEvolutionDashboardService
{
    public Task<EvolutionDashboardSummaryDto> GetSummaryAsync()
        => throw new NotImplementedException("Trilha B");
}
