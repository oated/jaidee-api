using JaiDee.Domain.Entities;

namespace JaiDee.Application.Interfaces.Repositories;

public interface ITransactionRepository
{
  Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
  Task<IReadOnlyList<Transaction>> GetByUserAndMonthAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
  Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
