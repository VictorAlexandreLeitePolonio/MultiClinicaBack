using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Authorization;
using MultiClinica.API.Common;
using MultiClinica.API.Services.Interfaces;

namespace MultiClinica.API.Controllers;

[Authorize]
[ApiController]
[Route("api/financeiro/auditoria")]
public class AuditoriaFinanceiraController(IAuditoriaFinanceiraService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.Auditoria.Visualizar)]
    public async Task<IActionResult> Listar(
        [FromQuery] string? modulo,
        [FromQuery] string? entidade,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok((await service.ListarAsync(modulo, entidade, dataInicio, dataFim, page, pageSize)).Value);
}
