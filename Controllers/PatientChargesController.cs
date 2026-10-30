using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Services.MercadoPago;

namespace MultiClinica.API.Controllers;

// "Minhas cobranças" no portal do paciente.
[ApiController]
[Route("api/patient/payments")]
[Authorize(AuthenticationSchemes = AuthSchemes.PatientAuth)]
public class PatientChargesController(OnlinePaymentService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok((await service.GetPatientChargesAsync(ct)).Value);
}
