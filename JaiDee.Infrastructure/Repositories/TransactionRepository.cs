using JaiDee.Application.Interfaces.Repositories;
using JaiDee.Domain.Entities;
using JaiDee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JaiDee.Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
  private readonly AppDbContext _dbContext;

  public TransactionRepository(AppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
  {
    return _dbContext.Transactions.AddAsync(transaction, cancellationToken).AsTask();
  }

  public async Task<IReadOnlyList<Transaction>> GetByUserAndMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
  {
    var periodStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    var nextPeriodStart = periodStart.AddMonths(1);

    return await _dbContext.Transactions
      .AsNoTracking()
      .Where(t => t.UserId == userId && t.TransactionDate >= periodStart && t.TransactionDate < nextPeriodStart)
      .OrderByDescending(t => t.TransactionDate)
      .ToListAsync(cancellationToken);
  }

  public Task SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    return _dbContext.SaveChangesAsync(cancellationToken);
  }
}
