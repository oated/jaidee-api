using System.Globalization;
using JaiDee.Application.DTOs;
using JaiDee.Application.Interfaces;
using JaiDee.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace JaiDee.API.Controllers;

[ApiController]
[Route("api/webhook/line")]
public class LineWebhookController : ControllerBase
{
  private readonly ITransactionService _transactionService;

  public LineWebhookController(ITransactionService transactionService)
  {
    _transactionService = transactionService;
  }

  [HttpPost]
  public async Task<IActionResult> HandleWebhook([FromBody] LineWebhookRequest request, CancellationToken cancellationToken)
  {
    if (request.Events.Count == 0)
    {
      return Ok(new { replies = Array.Empty<string>() });
    }

    var replies = new List<string>();
    foreach (var lineEvent in request.Events)
    {
      if (!string.Equals(lineEvent.Type, "message", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var text = lineEvent.Message?.Text?.Trim();
      if (string.IsNullOrWhiteSpace(text))
      {
        continue;
      }

      if (!TryParseTransaction(text, lineEvent.Source?.UserId, out var command))
      {
        replies.Add("พิมพ์แบบนี้ได้เลย: -120 ค่ากาแฟ หรือ +2000 เงินเดือน");
        continue;
      }

      var result = await _transactionService.RecordTransactionAsync(command, cancellationToken);
      replies.Add(
        "บันทึกแล้วนะ 💚\n" +
        $"รายรับเดือนนี้ {result.MonthlyIncome:0.##} บาท | รายจ่ายเดือนนี้ {result.MonthlyExpense:0.##} บาท\n" +
        "ค่อย ๆ ไปก็ได้นะ 🌿");
    }

    return Ok(new { replies });
  }

  private static bool TryParseTransaction(string text, string? lineUserId, out RecordTransactionCommand command)
  {
    command = new RecordTransactionCommand();
    if (string.IsNullOrWhiteSpace(lineUserId))
    {
      return false;
    }

    var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (tokens.Length == 0)
    {
      return false;
    }

    var firstToken = tokens[0];
    var type = GetTypeFromToken(firstToken);
    string amountToken;
    int noteStartIndex;

    if (firstToken.StartsWith('+') || firstToken.StartsWith('-'))
    {
      amountToken = firstToken;
      noteStartIndex = 1;
    }
    else if (type is not null && tokens.Length > 1)
    {
      amountToken = tokens[1];
      noteStartIndex = 2;
    }
    else
    {
      return false;
    }

    if (type is null)
    {
      return false;
    }

    amountToken = amountToken.Replace("+", string.Empty).Replace("-", string.Empty);
    if (!decimal.TryParse(amountToken, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
    {
      return false;
    }

    var note = tokens.Length > noteStartIndex ? string.Join(' ', tokens.Skip(noteStartIndex)) : string.Empty;

    command = new RecordTransactionCommand
    {
      LineUserId = lineUserId,
      DisplayName = lineUserId,
      Type = type.Value,
      Amount = Math.Abs(amount),
      Note = note,
      TransactionDate = DateTime.UtcNow
    };

    return true;
  }

  private static TransactionType? GetTypeFromToken(string token)
  {
    if (token.StartsWith('+') || token.Equals("income", StringComparison.OrdinalIgnoreCase) || token.Equals("รับ", StringComparison.OrdinalIgnoreCase))
    {
      return TransactionType.Income;
    }

    if (token.StartsWith('-') || token.Equals("expense", StringComparison.OrdinalIgnoreCase) || token.Equals("จ่าย", StringComparison.OrdinalIgnoreCase))
    {
      return TransactionType.Expense;
    }

    return null;
  }
}

public class LineWebhookRequest
{
  public List<LineWebhookEvent> Events { get; set; } = new();
}

public class LineWebhookEvent
{
  public string Type { get; set; } = string.Empty;
  public LineWebhookSource? Source { get; set; }
  public LineWebhookMessage? Message { get; set; }
}

public class LineWebhookSource
{
  public string UserId { get; set; } = string.Empty;
}

public class LineWebhookMessage
{
  public string Type { get; set; } = string.Empty;
  public string Text { get; set; } = string.Empty;
}
