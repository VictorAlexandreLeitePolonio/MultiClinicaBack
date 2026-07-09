using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/auditoria-financeira")]
public class AuditoriaFinanceiraController(IAuditoriaFinanceiraService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.Auditoria.Visualizar)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? modulo,
        [FromQuery] string? entidade,
        [FromQuery] int? usuarioId,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok((await service.GetPagedAsync(modulo, entidade, usuarioId, de, ate, page, pageSize)).Value);
}
