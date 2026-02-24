using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JaiDee.Application.DTOs;
using JaiDee.Application.Interfaces;
using JaiDee.Application.Interfaces.Repositories;
using JaiDee.API.Line;
using JaiDee.Domain.Entities;
using JaiDee.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JaiDee.API.Controllers;

[ApiController]
[Route("api/webhook/line")]
public class LineWebhookController : ControllerBase
{
  private static readonly TimeSpan ConversationTimeout = TimeSpan.FromMinutes(5);

  private readonly ITransactionService _transactionService;
  private readonly IConversationStateRepository _conversationStateRepository;
  private readonly ILineMessagingClient _lineMessagingClient;
  private readonly LineBotOptions _lineBotOptions;
  private readonly ILogger<LineWebhookController> _logger;

  [ActivatorUtilitiesConstructor]
  public LineWebhookController(
    ITransactionService transactionService,
    IConversationStateRepository conversationStateRepository,
    ILineMessagingClient lineMessagingClient,
    IOptions<LineBotOptions> lineBotOptions,
    ILogger<LineWebhookController> logger)
  {
    _transactionService = transactionService;
    _conversationStateRepository = conversationStateRepository;
    _lineMessagingClient = lineMessagingClient;
    _lineBotOptions = lineBotOptions.Value;
    _logger = logger;
  }

  [HttpPost]
  public async Task<IActionResult> HandleWebhook(CancellationToken cancellationToken)
  {
    Request.EnableBuffering();
    var rawBody = await ReadRequestBodyAsync(cancellationToken);

    if (!IsLineSignatureValid(rawBody))
    {
      return Unauthorized(new { message = "Invalid LINE signature." });
    }

    var request = JsonSerializer.Deserialize<LineWebhookRequest>(rawBody, new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true
    });

    if (request is null)
    {
      return BadRequest(new { message = "Invalid webhook payload." });
    }

    if (request.Events.Count == 0)
    {
      return Ok(new { replies = Array.Empty<string>() });
    }

    var replies = new List<string>();
    foreach (var lineEvent in request.Events)
    {
      using var _ = _logger.BeginScope(new Dictionary<string, object?>
      {
        ["LineUserId"] = lineEvent.Source?.UserId,
        ["LineEventType"] = lineEvent.Type
      });

      if (string.Equals(lineEvent.Type, "postback", StringComparison.OrdinalIgnoreCase))
      {
        await HandlePostbackEventAsync(lineEvent, replies, cancellationToken);
        continue;
      }

      if (!string.Equals(lineEvent.Type, "message", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      var text = lineEvent.Message?.Text?.Trim();
      if (string.IsNullOrWhiteSpace(text))
      {
        continue;
      }

      if (TryParseSummaryCommand(text, out var summaryYear, out var summaryMonth))
      {
        var lineUserId = lineEvent.Source?.UserId;
        if (string.IsNullOrWhiteSpace(lineUserId))
        {
          continue;
        }

        var summary = await _transactionService.GetMonthlySummaryAsync(lineUserId, summaryYear, summaryMonth, cancellationToken);
        var summaryReply = summary is null
          ? $"ยังไม่มีรายการสำหรับ {summaryMonth:D2}/{summaryYear} นะ"
          : BuildSummaryReply(summary);

        await ReplySummaryFlexIfPossibleAsync(lineEvent.ReplyToken, summary, summaryYear, summaryMonth, summaryReply, cancellationToken);
        replies.Add(summaryReply);
        continue;
      }

      if (TryParseConversationStartCommand(text, out var startType))
      {
        var lineUserId = lineEvent.Source?.UserId;
        if (string.IsNullOrWhiteSpace(lineUserId))
        {
          continue;
        }

        var startReply = await StartConversationAsync(lineUserId, startType, cancellationToken);
        await ReplyTextWithQuickRepliesIfPossibleAsync(lineEvent.ReplyToken, startReply, BuildNoteQuickReplies(startType), cancellationToken);
        replies.Add(startReply);
        continue;
      }

      var conversationHandled = await HandleConversationStepAsync(lineEvent, text, replies, cancellationToken);
      if (conversationHandled)
      {
        continue;
      }

      if (!TryParseTransaction(text, lineEvent.Source?.UserId, out var command))
      {
        var invalidFormatReply = "พิมพ์แบบนี้ได้เลย: -120 ค่ากาแฟ หรือ +2000 เงินเดือน";
        await ReplyIfPossibleAsync(lineEvent.ReplyToken, invalidFormatReply, cancellationToken);
        replies.Add(invalidFormatReply);
        continue;
      }

      var result = await _transactionService.RecordTransactionAsync(command, cancellationToken);
      var replyMessage =
        "บันทึกแล้วนะ 💚\n" +
        $"รายรับเดือนนี้ {result.MonthlyIncome:0.##} บาท | รายจ่ายเดือนนี้ {result.MonthlyExpense:0.##} บาท\n" +
        "ค่อย ๆ ไปก็ได้นะ 🌿";

      await ReplySuccessFlexIfPossibleAsync(lineEvent.ReplyToken, command, result, replyMessage, cancellationToken);
      replies.Add(replyMessage);
    }

    return Ok(new { replies });
  }

  private async Task HandlePostbackEventAsync(LineWebhookEvent lineEvent, List<string> replies, CancellationToken cancellationToken)
  {
    var lineUserId = lineEvent.Source?.UserId;
    var postbackData = lineEvent.Postback?.Data;
    if (string.IsNullOrWhiteSpace(lineUserId) || string.IsNullOrWhiteSpace(postbackData))
    {
      return;
    }

    if (!TryParseRecordPostback(postbackData, out var transactionType))
    {
      return;
    }

    var prompt = await StartConversationAsync(lineUserId, transactionType, cancellationToken);
    await ReplyTextWithQuickRepliesIfPossibleAsync(lineEvent.ReplyToken, prompt, BuildNoteQuickReplies(transactionType), cancellationToken);
    replies.Add(prompt);
  }

  private async Task<string> StartConversationAsync(string lineUserId, TransactionType transactionType, CancellationToken cancellationToken)
  {
    var state = await _conversationStateRepository.GetByLineUserIdAsync(lineUserId, cancellationToken);
    var isNewState = state is null;
    if (state is null)
    {
      state = new ConversationState
      {
        LineUserId = lineUserId
      };
      await _conversationStateRepository.AddAsync(state, cancellationToken);
    }

    state.Step = ConversationStep.AwaitingNote;
    state.PendingType = transactionType;
    state.PendingNote = string.Empty;
    state.PendingAmount = null;
    state.ExpiresAtUtc = DateTime.UtcNow.Add(ConversationTimeout);

    if (!isNewState)
    {
      _conversationStateRepository.Update(state);
    }

    await _conversationStateRepository.SaveChangesAsync(cancellationToken);

    var modeText = transactionType == TransactionType.Income ? "รายรับ" : "รายจ่าย";
    return $"โอเค เริ่มบันทึก{modeText}แล้ว\nพิมพ์ชื่อรายการได้เลย";
  }

  private async Task<bool> HandleConversationStepAsync(
    LineWebhookEvent lineEvent,
    string text,
    List<string> replies,
    CancellationToken cancellationToken)
  {
    var lineUserId = lineEvent.Source?.UserId;
    if (string.IsNullOrWhiteSpace(lineUserId))
    {
      return false;
    }

    var state = await _conversationStateRepository.GetByLineUserIdAsync(lineUserId, cancellationToken);
    if (state is null || state.Step == ConversationStep.None)
    {
      return false;
    }

    if (state.ExpiresAtUtc < DateTime.UtcNow)
    {
      _conversationStateRepository.Remove(state);
      await _conversationStateRepository.SaveChangesAsync(cancellationToken);
      var expiredReply = "หมดเวลาแล้ว เริ่มใหม่จาก Rich Menu อีกครั้งนะ";
      await ReplyIfPossibleAsync(lineEvent.ReplyToken, expiredReply, cancellationToken);
      replies.Add(expiredReply);
      return true;
    }

    if (IsCancelCommand(text))
    {
      _conversationStateRepository.Remove(state);
      await _conversationStateRepository.SaveChangesAsync(cancellationToken);
      const string cancelReply = "ยกเลิกการบันทึกแล้ว";
      await ReplyIfPossibleAsync(lineEvent.ReplyToken, cancelReply, cancellationToken);
      replies.Add(cancelReply);
      return true;
    }

    switch (state.Step)
    {
      case ConversationStep.AwaitingNote:
      {
        state.PendingNote = text;
        state.Step = ConversationStep.AwaitingAmount;
        state.ExpiresAtUtc = DateTime.UtcNow.Add(ConversationTimeout);
        _conversationStateRepository.Update(state);
        await _conversationStateRepository.SaveChangesAsync(cancellationToken);

        const string askAmountReply = "เท่าไหร่? (เช่น 200)";
        await ReplyAskAmountPromptIfPossibleAsync(lineEvent.ReplyToken, state.PendingType, state.PendingNote, askAmountReply, cancellationToken);
        replies.Add(askAmountReply);
        return true;
      }
      case ConversationStep.AwaitingAmount:
      {
        if (!TryParseAmount(text, out var amount))
        {
          const string invalidAmountReply = "จำนวนเงินไม่ถูกต้อง ลองพิมพ์ใหม่ เช่น 200";
          await ReplyIfPossibleAsync(lineEvent.ReplyToken, invalidAmountReply, cancellationToken);
          replies.Add(invalidAmountReply);
          return true;
        }

        state.PendingAmount = amount;
        state.Step = ConversationStep.AwaitingConfirmation;
        state.ExpiresAtUtc = DateTime.UtcNow.Add(ConversationTimeout);
        _conversationStateRepository.Update(state);
        await _conversationStateRepository.SaveChangesAsync(cancellationToken);

        var typeText = state.PendingType == TransactionType.Income ? "รายรับ" : "รายจ่าย";
        var confirmReply = $"ยืนยันบันทึก{typeText}\n{state.PendingNote} {amount:N0}฿\nตอบ 'ใช่' เพื่อยืนยัน หรือ 'ยกเลิก'";
        await ReplyConfirmPromptIfPossibleAsync(lineEvent.ReplyToken, state.PendingType, state.PendingNote, amount, confirmReply, cancellationToken);
        replies.Add(confirmReply);
        return true;
      }
      case ConversationStep.AwaitingConfirmation:
      {
        if (!IsConfirmCommand(text))
        {
          const string askConfirmReply = "ตอบ 'ใช่' เพื่อยืนยัน หรือพิมพ์ 'ยกเลิก'";
          await ReplyTextWithQuickRepliesIfPossibleAsync(lineEvent.ReplyToken, askConfirmReply, new[] { "ใช่", "ยกเลิก" }, cancellationToken);
          replies.Add(askConfirmReply);
          return true;
        }

        if (state.PendingType is null || state.PendingAmount is null)
        {
          _conversationStateRepository.Remove(state);
          await _conversationStateRepository.SaveChangesAsync(cancellationToken);
          const string invalidStateReply = "ข้อมูลไม่ครบ เริ่มใหม่จาก Rich Menu อีกครั้งนะ";
          await ReplyIfPossibleAsync(lineEvent.ReplyToken, invalidStateReply, cancellationToken);
          replies.Add(invalidStateReply);
          return true;
        }

        var command = new RecordTransactionCommand
        {
          LineUserId = lineUserId,
          DisplayName = lineUserId,
          Type = state.PendingType.Value,
          Amount = state.PendingAmount.Value,
          Note = state.PendingNote
        };

        var result = await _transactionService.RecordTransactionAsync(command, cancellationToken);
        var replyMessage =
          "บันทึกแล้วนะ 💚\n" +
          $"รายรับเดือนนี้ {result.MonthlyIncome:0.##} บาท | รายจ่ายเดือนนี้ {result.MonthlyExpense:0.##} บาท\n" +
          "ค่อย ๆ ไปก็ได้นะ 🌿";

        _conversationStateRepository.Remove(state);
        await _conversationStateRepository.SaveChangesAsync(cancellationToken);

        await ReplySuccessFlexIfPossibleAsync(lineEvent.ReplyToken, command, result, replyMessage, cancellationToken);
        replies.Add(replyMessage);
        return true;
      }
      default:
        return false;
    }
  }

  private async Task ReplySuccessFlexIfPossibleAsync(
    string? replyToken,
    RecordTransactionCommand command,
    RecordTransactionResult result,
    string fallbackMessage,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(replyToken) || string.IsNullOrWhiteSpace(_lineBotOptions.ChannelAccessToken))
    {
      return;
    }

    var isIncome = command.Type == TransactionType.Income;
    var title = isIncome ? "เงินเข้า" : "เงินออก";
    var titleColor = isIncome ? "#1DB446" : "#E53935";
    var subtitle = isIncome ? "บันทึกรายรับ" : "บันทึกรายจ่าย";
    var encouragement = isIncome ? "เยี่ยมเลยยย!" : "ค่อย ๆ วางแผนไปด้วยกันนะ";
    var note = string.IsNullOrWhiteSpace(command.Note) ? "-" : command.Note;
    var amount = $"{command.Amount:N0}฿";
    var transactionId = $"#{result.TransactionId.ToString("N")[..12]}";

    var flexContents = new
    {
      type = "bubble",
      body = new
      {
        type = "box",
        layout = "vertical",
        contents = new object[]
        {
          new
          {
            type = "text",
            text = title,
            weight = "bold",
            color = titleColor,
            size = "xxl"
          },
          new
          {
            type = "text",
            text = subtitle,
            size = "sm",
            color = "#666666",
            margin = "sm"
          },
          new
          {
            type = "text",
            text = encouragement,
            margin = "md"
          },
          new
          {
            type = "separator",
            margin = "xxl"
          },
          new
          {
            type = "box",
            layout = "vertical",
            margin = "xxl",
            spacing = "sm",
            contents = new object[]
            {
              new
              {
                type = "box",
                layout = "horizontal",
                contents = new object[]
                {
                  new
                  {
                    type = "text",
                    text = note,
                    size = "sm",
                    color = "#555555",
                    flex = 0
                  },
                  new
                  {
                    type = "text",
                    text = amount,
                    size = "sm",
                    color = "#111111",
                    align = "end"
                  }
                }
              }
            }
          },
          new
          {
            type = "separator",
            margin = "xxl"
          },
          new
          {
            type = "box",
            layout = "horizontal",
            margin = "md",
            contents = new object[]
            {
              new
              {
                type = "text",
                text = "Transaction ID",
                size = "xs",
                color = "#aaaaaa",
                flex = 0
              },
              new
              {
                type = "text",
                text = transactionId,
                color = "#aaaaaa",
                size = "xs",
                align = "end"
              }
            }
          }
        }
      },
      styles = new
      {
        footer = new
        {
          separator = true
        }
      }
    };

    try
    {
      await _lineMessagingClient.ReplyFlexAsync(replyToken, fallbackMessage, flexContents, CancellationToken.None);
    }
    catch
    {
      try
      {
        await _lineMessagingClient.ReplyTextAsync(replyToken, fallbackMessage, CancellationToken.None);
      }
      catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
      {
        _logger.LogWarning(ex, "LINE reply failed after flex fallback. userId={LineUserId}", HttpContext?.TraceIdentifier);
      }
    }
  }

  private async Task ReplyAskAmountPromptIfPossibleAsync(
    string? replyToken,
    TransactionType? transactionType,
    string note,
    string fallbackMessage,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(replyToken) || string.IsNullOrWhiteSpace(_lineBotOptions.ChannelAccessToken))
    {
      return;
    }

    var typeText = transactionType == TransactionType.Income ? "รายรับ" : "รายจ่าย";
    var prompt = $"รายการ: {note}\nเท่าไหร่สำหรับ{typeText}นี้? (เช่น 200)";
    await ReplyTextWithQuickRepliesIfPossibleAsync(replyToken, prompt, new[] { "100", "200", "500", "ยกเลิก" }, cancellationToken);
  }

  private async Task ReplyConfirmPromptIfPossibleAsync(
    string? replyToken,
    TransactionType? transactionType,
    string note,
    decimal amount,
    string fallbackMessage,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(replyToken) || string.IsNullOrWhiteSpace(_lineBotOptions.ChannelAccessToken))
    {
      return;
    }

    var typeText = transactionType == TransactionType.Income ? "รายรับ" : "รายจ่าย";
    var prompt = $"ยืนยันบันทึก{typeText}\n{note} {amount:N0}฿";
    await ReplyTextWithQuickRepliesIfPossibleAsync(replyToken, prompt, new[] { "ใช่", "ยกเลิก" }, cancellationToken);
  }

  private async Task ReplySummaryFlexIfPossibleAsync(
    string? replyToken,
    MonthlySummaryDto? summary,
    int year,
    int month,
    string fallbackMessage,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(replyToken) || string.IsNullOrWhiteSpace(_lineBotOptions.ChannelAccessToken))
    {
      return;
    }

    var hasData = summary is not null;
    var income = hasData ? summary!.TotalIncome : 0m;
    var expense = hasData ? summary!.TotalExpense : 0m;
    var balance = hasData ? summary!.Balance : 0m;
    var count = hasData ? summary!.TransactionCount : 0;
    var titleColor = balance >= 0 ? "#1DB446" : "#E53935";
    var subtitle = hasData ? "สรุปรายเดือน" : "ยังไม่มีรายการในเดือนนี้";

    var flexContents = new
    {
      type = "bubble",
      body = new
      {
        type = "box",
        layout = "vertical",
        contents = new object[]
        {
          new
          {
            type = "text",
            text = $"สรุป {month:D2}/{year}",
            weight = "bold",
            color = titleColor,
            size = "xl"
          },
          new
          {
            type = "text",
            text = subtitle,
            size = "sm",
            color = "#666666",
            margin = "sm"
          },
          new
          {
            type = "separator",
            margin = "lg"
          },
          new
          {
            type = "box",
            layout = "vertical",
            margin = "lg",
            spacing = "sm",
            contents = new object[]
            {
              BuildSummaryRow("รายรับ", $"{income:N0}฿"),
              BuildSummaryRow("รายจ่าย", $"{expense:N0}฿"),
              BuildSummaryRow("คงเหลือ", $"{balance:N0}฿"),
              BuildSummaryRow("จำนวนรายการ", count.ToString(CultureInfo.InvariantCulture))
            }
          }
        }
      }
    };

    try
    {
      await _lineMessagingClient.ReplyFlexAsync(replyToken, fallbackMessage, flexContents, CancellationToken.None);
    }
    catch
    {
      try
      {
        await _lineMessagingClient.ReplyTextAsync(replyToken, fallbackMessage, CancellationToken.None);
      }
      catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
      {
        _logger.LogWarning(ex, "LINE summary reply failed after flex fallback. traceId={TraceId}", HttpContext?.TraceIdentifier);
      }
    }
  }

  private async Task ReplyIfPossibleAsync(string? replyToken, string message, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(replyToken))
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(_lineBotOptions.ChannelAccessToken))
    {
      return;
    }

    try
    {
      await _lineMessagingClient.ReplyTextAsync(replyToken, message, CancellationToken.None);
    }
    catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
    {
      _logger.LogWarning(ex, "LINE text reply failed. traceId={TraceId}", HttpContext?.TraceIdentifier);
    }
  }

  private async Task ReplyTextWithQuickRepliesIfPossibleAsync(
    string? replyToken,
    string message,
    IReadOnlyList<string> quickReplies,
    CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(replyToken))
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(_lineBotOptions.ChannelAccessToken))
    {
      return;
    }

    try
    {
      await _lineMessagingClient.ReplyTextWithQuickRepliesAsync(replyToken, message, quickReplies, CancellationToken.None);
    }
    catch (Exception ex) when (ex is TaskCanceledException || ex is HttpRequestException)
    {
      _logger.LogWarning(ex, "LINE quick reply failed. traceId={TraceId}", HttpContext?.TraceIdentifier);
    }
  }

  private async Task<string> ReadRequestBodyAsync(CancellationToken cancellationToken)
  {
    Request.Body.Position = 0;
    using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
    var rawBody = await reader.ReadToEndAsync(cancellationToken);
    Request.Body.Position = 0;
    return rawBody;
  }

  private bool IsLineSignatureValid(string rawBody)
  {
    if (_lineBotOptions.SkipSignatureValidation)
    {
      return true;
    }

    if (string.IsNullOrWhiteSpace(_lineBotOptions.ChannelSecret))
    {
      return false;
    }

    if (!Request.Headers.TryGetValue("x-line-signature", out var signatureHeader))
    {
      return false;
    }

    var signature = signatureHeader.ToString();
    if (string.IsNullOrWhiteSpace(signature))
    {
      return false;
    }

    byte[] signatureBytes;
    try
    {
      signatureBytes = Convert.FromBase64String(signature);
    }
    catch (FormatException)
    {
      return false;
    }

    var bodyBytes = Encoding.UTF8.GetBytes(rawBody);
    var secretBytes = Encoding.UTF8.GetBytes(_lineBotOptions.ChannelSecret);

    using var hmac = new HMACSHA256(secretBytes);
    var computedSignature = hmac.ComputeHash(bodyBytes);

    return CryptographicOperations.FixedTimeEquals(computedSignature, signatureBytes);
  }

  private static bool TryParseSummaryCommand(string text, out int year, out int month)
  {
    var now = DateTime.UtcNow;
    year = now.Year;
    month = now.Month;

    var normalized = text.Trim().ToLowerInvariant();
    if (normalized is "summary" or "สรุป")
    {
      return true;
    }

    var match = Regex.Match(
      normalized,
      @"^(summary|สรุป)\s+((?<m>\d{1,2})/(?<y>\d{4})|(?<y2>\d{4})-(?<m2>\d{1,2}))$",
      RegexOptions.CultureInvariant);

    if (!match.Success)
    {
      return false;
    }

    var monthGroup = match.Groups["m"].Success ? match.Groups["m"].Value : match.Groups["m2"].Value;
    var yearGroup = match.Groups["y"].Success ? match.Groups["y"].Value : match.Groups["y2"].Value;

    if (!int.TryParse(monthGroup, out month) || !int.TryParse(yearGroup, out year))
    {
      return false;
    }

    return month is >= 1 and <= 12;
  }

  private static bool TryParseRecordPostback(string postbackData, out TransactionType transactionType)
  {
    var normalized = postbackData.Trim().ToLowerInvariant();
    if (normalized is "record_income" or "income" or "action=record&type=income"
        || normalized.Contains("type=income", StringComparison.Ordinal))
    {
      transactionType = TransactionType.Income;
      return true;
    }

    if (normalized is "record_expense" or "expense" or "action=record&type=expense"
        || normalized.Contains("type=expense", StringComparison.Ordinal))
    {
      transactionType = TransactionType.Expense;
      return true;
    }

    transactionType = default;
    return false;
  }

  private static bool TryParseConversationStartCommand(string text, out TransactionType transactionType)
  {
    var normalized = text.Trim().ToLowerInvariant();

    if (normalized is "รายรับ" or "record_income" or "income")
    {
      transactionType = TransactionType.Income;
      return true;
    }

    if (normalized is "รายจ่าย" or "record_expense" or "expense")
    {
      transactionType = TransactionType.Expense;
      return true;
    }

    transactionType = default;
    return false;
  }

  private static string BuildSummaryReply(MonthlySummaryDto summary)
  {
    return
      $"สรุป {summary.Month:D2}/{summary.Year}\n" +
      $"รายรับ: {summary.TotalIncome:N0}฿\n" +
      $"รายจ่าย: {summary.TotalExpense:N0}฿\n" +
      $"คงเหลือ: {summary.Balance:N0}฿\n" +
      $"จำนวนรายการ: {summary.TransactionCount}";
  }

  private static object BuildSummaryRow(string label, string value)
  {
    return new
    {
      type = "box",
      layout = "horizontal",
      contents = new object[]
      {
        new
        {
          type = "text",
          text = label,
          size = "sm",
          color = "#555555",
          flex = 0
        },
        new
        {
          type = "text",
          text = value,
          size = "sm",
          color = "#111111",
          align = "end"
        }
      }
    };
  }

  private static bool TryParseAmount(string text, out decimal amount)
  {
    var sanitized = text
      .Trim()
      .Replace(",", string.Empty)
      .Replace("฿", string.Empty)
      .Replace("บาท", string.Empty, StringComparison.OrdinalIgnoreCase);

    if (!decimal.TryParse(sanitized, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
    {
      return false;
    }

    amount = Math.Abs(amount);
    return amount > 0;
  }

  private static bool IsCancelCommand(string text)
  {
    var normalized = text.Trim().ToLowerInvariant();
    return normalized is "ยกเลิก" or "cancel" or "ไม่";
  }

  private static bool IsConfirmCommand(string text)
  {
    var normalized = text.Trim().ToLowerInvariant();
    return normalized is "ใช่" or "yes" or "ok" or "ตกลง" or "ยืนยัน";
  }

  private IReadOnlyList<string> BuildNoteQuickReplies(TransactionType transactionType)
  {
    var source = transactionType == TransactionType.Expense
      ? _lineBotOptions.ExpenseNoteSuggestions
      : _lineBotOptions.IncomeNoteSuggestions;

    var candidates = source
      .Where(x => !string.IsNullOrWhiteSpace(x))
      .Select(x => x.Trim())
      .Distinct(StringComparer.Ordinal)
      .ToList();

    // Randomize suggestion chips so repetitive sessions feel less rigid.
    for (var i = candidates.Count - 1; i > 0; i--)
    {
      var j = Random.Shared.Next(i + 1);
      (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
    }

    var take = _lineBotOptions.NoteQuickReplyCount <= 0 ? 3 : _lineBotOptions.NoteQuickReplyCount;
    var result = candidates.Take(take).ToList();
    result.Add("ยกเลิก");
    return result;
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
      Note = note
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
  public string ReplyToken { get; set; } = string.Empty;
  public LineWebhookPostback? Postback { get; set; }
  public LineWebhookSource? Source { get; set; }
  public LineWebhookMessage? Message { get; set; }
}

public class LineWebhookPostback
{
  public string Data { get; set; } = string.Empty;
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
