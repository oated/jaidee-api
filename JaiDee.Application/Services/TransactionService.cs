using JaiDee.Application.DTOs;
using JaiDee.Application.Interfaces;
using JaiDee.Application.Interfaces.Repositories;
using JaiDee.Domain.Entities;
using JaiDee.Domain.Enums;

namespace JaiDee.Application.Services;

public class TransactionService : ITransactionService
{
  private readonly IUserRepository _userRepository;
  private readonly ITransactionRepository _transactionRepository;

  public TransactionService(IUserRepository userRepository, ITransactionRepository transactionRepository)
  {
    _userRepository = userRepository;
    _transactionRepository = transactionRepository;
  }

  public async Task<RecordTransactionResult> RecordTransactionAsync(RecordTransactionCommand command, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(command.LineUserId))
    {
      throw new ArgumentException("LineUserId is required.", nameof(command));
    }

    if (command.Amount <= 0)
    {
      throw new ArgumentException("Amount must be greater than 0.", nameof(command));
    }

    var user = await _userRepository.GetByLineUserIdAsync(command.LineUserId, cancellationToken);
    if (user is null)
    {
      user = new User
      {
        LineUserId = command.LineUserId,
        DisplayName = string.IsNullOrWhiteSpace(command.DisplayName) ? command.LineUserId : command.DisplayName
      };

      await _userRepository.AddAsync(user, cancellationToken);
    }
    else if (!string.IsNullOrWhiteSpace(command.DisplayName) && !string.Equals(user.DisplayName, command.DisplayName, StringComparison.Ordinal))
    {
      user.DisplayName = command.DisplayName;
    }

    var transaction = new Transaction
    {
      UserId = user.Id,
      Type = command.Type,
      Amount = command.Amount,
      Note = command.Note,
      TransactionDate = DateTime.UtcNow
    };

    await _transactionRepository.AddAsync(transaction, cancellationToken);
    await _transactionRepository.SaveChangesAsync(cancellationToken);

    var month = transaction.TransactionDate.Month;
    var year = transaction.TransactionDate.Year;
    var monthlyTransactions = await _transactionRepository.GetByUserAndMonthAsync(user.Id, year, month, cancellationToken);

    return new RecordTransactionResult
    {
      TransactionId = transaction.Id,
      MonthlyIncome = monthlyTransactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
      MonthlyExpense = monthlyTransactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
    };
  }

  public async Task<MonthlySummaryDto?> GetMonthlySummaryAsync(string lineUserId, int year, int month, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(lineUserId))
    {
      return null;
    }

    if (month is < 1 or > 12)
    {
      throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
    }

    var user = await _userRepository.GetByLineUserIdAsync(lineUserId, cancellationToken);
    if (user is null)
    {
      return null;
    }

    var transactions = await _transactionRepository.GetByUserAndMonthAsync(user.Id, year, month, cancellationToken);

    return new MonthlySummaryDto
    {
      Year = year,
      Month = month,
      TotalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
      TotalExpense = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
      TransactionCount = transactions.Count
    };
  }
}
