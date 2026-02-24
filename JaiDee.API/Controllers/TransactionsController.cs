using JaiDee.Application.DTOs;
using JaiDee.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JaiDee.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
  private readonly ITransactionService _transactionService;

  public TransactionsController(ITransactionService transactionService)
  {
    _transactionService = transactionService;
  }

  [HttpPost]
  public async Task<ActionResult<RecordTransactionResult>> RecordTransaction(
    [FromBody] RecordTransactionCommand command,
    CancellationToken cancellationToken)
  {
    var result = await _transactionService.RecordTransactionAsync(command, cancellationToken);
    return Ok(result);
  }

  [HttpGet("monthly-summary")]
  public async Task<ActionResult<MonthlySummaryDto>> GetMonthlySummary(
    [FromQuery] string lineUserId,
    [FromQuery] int year,
    [FromQuery] int month,
    CancellationToken cancellationToken)
  {
    var summary = await _transactionService.GetMonthlySummaryAsync(lineUserId, year, month, cancellationToken);
    if (summary is null)
    {
      return NotFound();
    }

    return Ok(summary);
  }
}
