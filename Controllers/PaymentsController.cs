using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiClinica.API.Common;
using MultiClinica.API.DTOs;
using MultiClinica.API.DTOs.Payment;
using MultiClinica.API.Models;
using MultiClinica.API.Services.Interfaces;
using MultiClinica.API.Services.MercadoPago;

namespace MultiClinica.API.Controllers;

[Authorize(Roles = "Administrador,Recepcao")]
[ApiController]
[Route("api/[controller]")]
public class PaymentsController(
    IPaymentService service,
    OnlinePaymentService onlinePayments,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int? patientId,
        [FromQuery] PaymentStatus? status,
        [FromQuery] string? referenceMonth,
        [FromQuery] string? PatientName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        DateOnly? parsedReferenceMonth = null;
        if (!string.IsNullOrWhiteSpace(referenceMonth))
        {
            if (!DateOnly.TryParseExact(
                    referenceMonth,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
                return BadRequest(new { message = "O mês de referência deve usar o formato YYYY-MM-DD." });

            parsedReferenceMonth = parsed;
        }

        var result = await service.GetPagedAsync(
            patientId, status, parsedReferenceMonth, PatientName, page, pageSize);
        return Ok(result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(int id)
    {
        var result = await service.GetByIdAsync(id);
        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(new { message = result.ErrorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> CreatePayment(CreatePaymentDto dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound         => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.DuplicatePayment => Conflict(new { message = result.ErrorMessage }),
                _                           => BadRequest(new { message = result.ErrorMessage, code = result.ErrorCode })
            };

        var created = result.Value!;

        // O envio fica no controller para o PaymentService não depender do OnlinePaymentService.
        // Se o envio falhar, a cobrança continua criada e o erro só é logado.
        if (PaymentMethods.IsOnline(dto.PaymentMethod) && dto.SendOnline)
        {
            try
            {
                var sent = await onlinePayments.SendAsync(created.Id, ct);
                if (sent.IsSuccess)
                {
                    // Devolve o token/estado já atualizados pelo envio.
                    var refreshed = await service.GetByIdAsync(created.Id);
                    if (refreshed.IsSuccess)
                        created = refreshed.Value!;
                }
                else
                {
                    logger.LogError(
                        "Falha ao enviar a cobrança {PaymentId} após criá-la: {Code} {Message}",
                        created.Id, sent.ErrorCode, sent.ErrorMessage);
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Falha ao enviar a cobrança {PaymentId} após criá-la.", created.Id);
            }
        }

        return CreatedAtAction(nameof(GetPayment), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePayment(int id, UpdatePaymentDto dto)
    {
        var result = await service.UpdateAsync(id, dto);
        if (!result.IsSuccess)
            return result.ErrorCode switch
            {
                ErrorCodes.NotFound       => NotFound(new { message = result.ErrorMessage }),
                ErrorCodes.DuplicatePayment => Conflict(new { message = result.ErrorMessage }),
                _                         => BadRequest(new { message = result.ErrorMessage, code = result.ErrorCode })
            };

        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePayment(int id)
    {
        var result = await service.DeleteAsync(id);
        if (!result.IsSuccess)
            return result.ErrorCode == ErrorCodes.NotFound
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage, code = result.ErrorCode });

        return NoContent();
    }
}
