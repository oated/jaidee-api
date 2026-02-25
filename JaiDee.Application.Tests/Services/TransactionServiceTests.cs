using JaiDee.Application.DTOs;
using JaiDee.Application.Interfaces.Repositories;
using JaiDee.Application.Services;
using JaiDee.Domain.Entities;
using JaiDee.Domain.Enums;
using Xunit;

namespace JaiDee.Application.Tests.Services;

public class TransactionServiceTests
{
  [Fact]
  public async Task RecordTransactionAsync_Throws_WhenLineUserIdIsMissing()
  {
    var service = CreateService(out _, out _);

    await Assert.ThrowsAsync<ArgumentException>(() => service.RecordTransactionAsync(new RecordTransactionCommand
    {
      LineUserId = "",
      Amount = 100,
      Type = TransactionType.Expense
    }));
  }

  [Fact]
  public async Task RecordTransactionAsync_Throws_WhenAmountIsNotPositive()
  {
    var service = CreateService(out _, out _);

    await Assert.ThrowsAsync<ArgumentException>(() => service.RecordTransactionAsync(new RecordTransactionCommand
    {
      LineUserId = "u_test",
      Amount = 0,
      Type = TransactionType.Expense
    }));
  }

  [Fact]
  public async Task RecordTransactionAsync_CreatesUserAndReturnsMonthlyTotals()
  {
    var service = CreateService(out var userRepository, out var transactionRepository);

    var result = await service.RecordTransactionAsync(new RecordTransactionCommand
    {
      LineUserId = "u_new",
      DisplayName = "Nat",
      Type = TransactionType.Expense,
      Amount = 120,
      Note = "coffee"
    });

    var user = await userRepository.GetByLineUserIdAsync("u_new");

    Assert.NotNull(user);
    Assert.Equal("Nat", user!.DisplayName);
    Assert.Equal(1, userRepository.AddedCount);
    Assert.Equal(1, transactionRepository.SaveChangesCount);
    Assert.True(result.TransactionId != Guid.Empty);
    Assert.Equal(0, result.MonthlyIncome);
    Assert.Equal(120, result.MonthlyExpense);
  }

  [Fact]
  public async Task RecordTransactionAsync_UpdatesDisplayName_WhenUserExists()
  {
    var existingUser = new User
    {
      LineUserId = "u_existing",
      DisplayName = "Old Name"
    };

    var service = CreateService(out var userRepository, out _, existingUser);

    await service.RecordTransactionAsync(new RecordTransactionCommand
    {
      LineUserId = "u_existing",
      DisplayName = "New Name",
      Type = TransactionType.Income,
      Amount = 1000,
      Note = "salary"
    });

    var user = await userRepository.GetByLineUserIdAsync("u_existing");
    Assert.NotNull(user);
    Assert.Equal("New Name", user!.DisplayName);
    Assert.Equal(0, userRepository.AddedCount);
  }

  [Fact]
  public async Task GetMonthlySummaryAsync_ReturnsNull_WhenLineUserIdIsEmpty()
  {
    var service = CreateService(out _, out _);
    var result = await service.GetMonthlySummaryAsync("", 2026, 2);
    Assert.Null(result);
  }

  [Fact]
  public async Task GetMonthlySummaryAsync_Throws_WhenMonthOutOfRange()
  {
    var service = CreateService(out _, out _);

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetMonthlySummaryAsync("u_test", 2026, 13));
  }

  [Fact]
  public async Task GetMonthlySummaryAsync_ReturnsCorrectAggregate()
  {
    var user = new User
    {
      LineUserId = "u_summary",
      DisplayName = "Nat"
    };

    var service = CreateService(out _, out var transactionRepository, user);
    var targetYear = 2026;
    var targetMonth = 2;

    transactionRepository.Seed(new Transaction
    {
      UserId = user.Id,
      Type = TransactionType.Income,
      Amount = 20000,
      Note = "salary",
      TransactionDate = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc)
    });
    transactionRepository.Seed(new Transaction
    {
      UserId = user.Id,
      Type = TransactionType.Expense,
      Amount = 250,
      Note = "coffee",
      TransactionDate = new DateTime(targetYear, targetMonth, 2, 0, 0, 0, DateTimeKind.Utc)
    });
    transactionRepository.Seed(new Transaction
    {
      UserId = user.Id,
      Type = TransactionType.Expense,
      Amount = 2000,
      Note = "food",
      TransactionDate = new DateTime(targetYear, targetMonth, 10, 0, 0, 0, DateTimeKind.Utc)
    });

    var summary = await service.GetMonthlySummaryAsync("u_summary", targetYear, targetMonth);

    Assert.NotNull(summary);
    Assert.Equal(20000, summary!.TotalIncome);
    Assert.Equal(2250, summary.TotalExpense);
    Assert.Equal(17750, summary.Balance);
    Assert.Equal(3, summary.TransactionCount);
  }

  private static TransactionService CreateService(
    out FakeUserRepository userRepository,
    out FakeTransactionRepository transactionRepository,
    params User[] users)
  {
    userRepository = new FakeUserRepository(users);
    transactionRepository = new FakeTransactionRepository();
    return new TransactionService(userRepository, transactionRepository);
  }

  private sealed class FakeUserRepository : IUserRepository
  {
    private readonly Dictionary<string, User> _users;

    public int AddedCount { get; private set; }

    public FakeUserRepository(IEnumerable<User> users)
    {
      _users = users.ToDictionary(x => x.LineUserId, x => x, StringComparer.Ordinal);
    }

    public Task<User?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default)
    {
      _users.TryGetValue(lineUserId, out var user);
      return Task.FromResult(user);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
      AddedCount++;
      _users[user.LineUserId] = user;
      return Task.CompletedTask;
    }
  }

  private sealed class FakeTransactionRepository : ITransactionRepository
  {
    private readonly List<Transaction> _transactions = new();

    public int SaveChangesCount { get; private set; }

    public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
      _transactions.Add(transaction);
      return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Transaction>> GetByUserAndMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
      var list = _transactions
        .Where(t => t.UserId == userId && t.TransactionDate.Year == year && t.TransactionDate.Month == month)
        .ToList()
        .AsReadOnly();
      return Task.FromResult((IReadOnlyList<Transaction>)list);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveChangesCount++;
      return Task.CompletedTask;
    }

    public void Seed(Transaction transaction)
    {
      _transactions.Add(transaction);
    }
  }
}
