using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Clinic;
using MultiClinica.API.DTOs.Marketplace;

namespace MultiClinica.API.Services.Interfaces;

public interface IMarketplaceService
{
    Task<Result<IReadOnlyList<ClinicCategoryDto>>> GetCategoriesAsync();
    Task<Result<PagedResult<MarketplaceClinicCardDto>>> GetClinicsAsync(MarketplaceClinicQuery query);
    Task<Result<MarketplaceClinicDetailsDto>> GetClinicAsync(int clinicId);
}
