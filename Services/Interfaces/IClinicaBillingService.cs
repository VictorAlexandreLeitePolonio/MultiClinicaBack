namespace MultiClinica.API.Services.Interfaces;

public interface IClinicaBillingService
{
    Task GenerateMonthlyChargesAsync(DateOnly today, CancellationToken cancellationToken = default);
    Task BlockOverdueClinicsAsync(DateOnly today, CancellationToken cancellationToken = default);
}
