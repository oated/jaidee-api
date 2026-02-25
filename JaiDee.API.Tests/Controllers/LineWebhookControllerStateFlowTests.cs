using System.Text;
using System.Text.Json;
using JaiDee.API.Controllers;
using JaiDee.API.Line;
using JaiDee.Application.DTOs;
using JaiDee.Application.Interfaces;
using JaiDee.Application.Interfaces.Repositories;
using JaiDee.Domain.Entities;
using JaiDee.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace JaiDee.API.Tests.Controllers;

public class LineWebhookControllerStateFlowTests
{
  [Fact]
  public async Task StartFlow_WithExpenseCommand_CreatesAwaitingNoteState()
  {
    var transactionService = new FakeTransactionService();
    var stateRepository = new FakeConversationStateRepository();
    var lineClient = new FakeLineMessagingClient();
    var controller = CreateController(transactionService, stateRepository, lineClient);

    await InvokeMessageEventAsync(controller, "u_1", "r_1", "รายจ่าย");

    var state = await stateRepository.GetByLineUserIdAsync("u_1");
    Assert.NotNull(state);
    Assert.Equal(ConversationStep.AwaitingNote, state!.Step);
    Assert.Equal(TransactionType.Expense, state.PendingType);
    Assert.Equal(string.Empty, state.PendingNote);
    Assert.Null(state.PendingAmount);
    Assert.Contains(lineClient.Calls, c => c.Method == "ReplyTextWithQuickReplies" && c.QuickReplies.Contains("ยกเลิก"));
  }

  [Fact]
  public async Task ConversationFlow_NoteAmountConfirm_RecordsTransactionAndClearsState()
  {
    var transactionService = new FakeTransactionService();
    var stateRepository = new FakeConversationStateRepository();
    var lineClient = new FakeLineMessagingClient();
    var controller = CreateController(transactionService, stateRepository, lineClient);

    await InvokeMessageEventAsync(controller, "u_2", "r_start", "รายจ่าย");
    await InvokeMessageEventAsync(controller, "u_2", "r_note", "ค่าข้าว");
    await InvokeMessageEventAsync(controller, "u_2", "r_amount", "200");
    await InvokeMessageEventAsync(controller, "u_2", "r_confirm", "ใช่");

    Assert.Single(transactionService.RecordedCommands);
    var recorded = transactionService.RecordedCommands[0];
    Assert.Equal("u_2", recorded.LineUserId);
    Assert.Equal(TransactionType.Expense, recorded.Type);
    Assert.Equal("ค่าข้าว", recorded.Note);
    Assert.Equal(200m, recorded.Amount);

    var state = await stateRepository.GetByLineUserIdAsync("u_2");
    Assert.Null(state);
    Assert.Contains(lineClient.Calls, c => c.Method == "ReplyFlex");
  }

  [Fact]
  public async Task ConversationFlow_CancelCommand_ClearsStateAndDoesNotRecord()
  {
    var transactionService = new FakeTransactionService();
    var stateRepository = new FakeConversationStateRepository();
    var lineClient = new FakeLineMessagingClient();
    var controller = CreateController(transactionService, stateRepository, lineClient);

    await InvokeMessageEventAsync(controller, "u_3", "r_start", "รายรับ");
    await InvokeMessageEventAsync(controller, "u_3", "r_cancel", "ยกเลิก");

    var state = await stateRepository.GetByLineUserIdAsync("u_3");
    Assert.Null(state);
    Assert.Empty(transactionService.RecordedCommands);
    Assert.Contains(lineClient.Calls, c => c.Method == "ReplyText" && c.Message.Contains("ยกเลิก"));
  }

  [Fact]
  public async Task ConversationFlow_ExpiredState_RemovesStateAndDoesNotRecord()
  {
    var transactionService = new FakeTransactionService();
    var stateRepository = new FakeConversationStateRepository();
    var lineClient = new FakeLineMessagingClient();
    var controller = CreateController(transactionService, stateRepository, lineClient);

    await stateRepository.AddAsync(new ConversationState
    {
      LineUserId = "u_4",
      Step = ConversationStep.AwaitingAmount,
      PendingType = TransactionType.Expense,
      PendingNote = "ค่าข้าว",
      ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
    });
    await stateRepository.SaveChangesAsync();

    await InvokeMessageEventAsync(controller, "u_4", "r_expired", "200");

    var state = await stateRepository.GetByLineUserIdAsync("u_4");
    Assert.Null(state);
    Assert.Empty(transactionService.RecordedCommands);
    Assert.Contains(lineClient.Calls, c => c.Method == "ReplyText" && c.Message.Contains("หมดเวลา"));
  }

  private static LineWebhookController CreateController(
    FakeTransactionService transactionService,
    FakeConversationStateRepository stateRepository,
    FakeLineMessagingClient lineClient)
  {
    var options = Options.Create(new LineBotOptions
    {
      SkipSignatureValidation = true,
      ChannelAccessToken = "token",
      ExpenseNoteSuggestions = new List<string> { "ค่าข้าว", "น้ำหวาน", "กาแฟ" },
      IncomeNoteSuggestions = new List<string> { "เงินเดือน", "พิเศษ" }
    });

    return new LineWebhookController(
      transactionService,
      stateRepository,
      lineClient,
      options,
      NullLogger<LineWebhookController>.Instance);
  }

  private static async Task InvokeMessageEventAsync(LineWebhookController controller, string lineUserId, string replyToken, string text)
  {
    var payload = new
    {
      events = new[]
      {
        new
        {
          type = "message",
          replyToken,
          source = new { userId = lineUserId },
          message = new { type = "text", text }
        }
      }
    };

    var json = JsonSerializer.Serialize(payload);
    var bodyBytes = Encoding.UTF8.GetBytes(json);
    var context = new DefaultHttpContext();
    context.Request.Body = new MemoryStream(bodyBytes);
    context.Request.ContentType = "application/json";
    context.Request.ContentLength = bodyBytes.Length;

    controller.ControllerContext = new ControllerContext
    {
      HttpContext = context
    };

    var result = await controller.HandleWebhook(CancellationToken.None);
    Assert.IsType<OkObjectResult>(result);
  }

  private sealed class FakeTransactionService : ITransactionService
  {
    public List<RecordTransactionCommand> RecordedCommands { get; } = new();

    public Task<RecordTransactionResult> RecordTransactionAsync(RecordTransactionCommand command, CancellationToken cancellationToken = default)
    {
      RecordedCommands.Add(command);
      var income = command.Type == TransactionType.Income ? command.Amount : 0m;
      var expense = command.Type == TransactionType.Expense ? command.Amount : 0m;
      return Task.FromResult(new RecordTransactionResult
      {
        TransactionId = Guid.NewGuid(),
        MonthlyIncome = income,
        MonthlyExpense = expense
      });
    }

    public Task<MonthlySummaryDto?> GetMonthlySummaryAsync(string lineUserId, int year, int month, CancellationToken cancellationToken = default)
    {
      return Task.FromResult<MonthlySummaryDto?>(new MonthlySummaryDto
      {
        Year = year,
        Month = month,
        TotalIncome = 1000,
        TotalExpense = 200,
        TransactionCount = 2
      });
    }
  }

  private sealed class FakeConversationStateRepository : IConversationStateRepository
  {
    private readonly Dictionary<string, ConversationState> _store = new(StringComparer.Ordinal);

    public Task<ConversationState?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default)
    {
      _store.TryGetValue(lineUserId, out var state);
      return Task.FromResult(state);
    }

    public Task AddAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
      _store[state.LineUserId] = state;
      return Task.CompletedTask;
    }

    public void Update(ConversationState state)
    {
      _store[state.LineUserId] = state;
    }

    public void Remove(ConversationState state)
    {
      _store.Remove(state.LineUserId);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      return Task.CompletedTask;
    }
  }

  private sealed class FakeLineMessagingClient : ILineMessagingClient
  {
    public List<CallRecord> Calls { get; } = new();

    public Task ReplyTextAsync(string replyToken, string message, CancellationToken cancellationToken = default)
    {
      Calls.Add(new CallRecord("ReplyText", replyToken, message, Array.Empty<string>()));
      return Task.CompletedTask;
    }

    public Task ReplyTextWithQuickRepliesAsync(string replyToken, string message, IReadOnlyList<string> quickReplies, CancellationToken cancellationToken = default)
    {
      Calls.Add(new CallRecord("ReplyTextWithQuickReplies", replyToken, message, quickReplies.ToArray()));
      return Task.CompletedTask;
    }

    public Task ReplyFlexAsync(string replyToken, string altText, object contents, CancellationToken cancellationToken = default)
    {
      Calls.Add(new CallRecord("ReplyFlex", replyToken, altText, Array.Empty<string>()));
      return Task.CompletedTask;
    }
  }

  private sealed record CallRecord(string Method, string ReplyToken, string Message, IReadOnlyList<string> QuickReplies);
}
