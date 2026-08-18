using MultiClinica.API.Common;
using MultiClinica.API.DTOs.Clinic;

namespace MultiClinica.API.Services.Interfaces;

public interface IClinicProfileService
{
    // Catálogo + categorias da clínica
    Task<Result<IReadOnlyList<ClinicCategoryDto>>> GetCategoryCatalogAsync();
    Task<Result<IReadOnlyList<ClinicCategoryDto>>> GetClinicCategoriesAsync();
    Task<Result<IReadOnlyList<ClinicCategoryDto>>> SetClinicCategoriesAsync(SetClinicCategoriesRequest request);

    // Horários de funcionamento
    Task<Result<IReadOnlyList<BusinessHourDto>>> GetBusinessHoursAsync();
    Task<Result<BusinessHourDto>> AddBusinessHourAsync(CreateBusinessHourRequest request);
    Task<Result<bool>> DeleteBusinessHourAsync(int id);

    // Perfil público
    Task<Result<PublicClinicDto>> GetPublicBySlugAsync(string slug);
}
