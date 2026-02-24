using JaiDee.Domain.Entities;

namespace JaiDee.Application.Interfaces.Repositories;

public interface IUserRepository
{
  Task<User?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default);
  Task AddAsync(User user, CancellationToken cancellationToken = default);
}
