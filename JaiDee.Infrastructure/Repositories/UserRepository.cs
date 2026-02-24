using JaiDee.Application.Interfaces.Repositories;
using JaiDee.Domain.Entities;
using JaiDee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JaiDee.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
  private readonly AppDbContext _dbContext;

  public UserRepository(AppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public Task<User?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default)
  {
    return _dbContext.Users
      .FirstOrDefaultAsync(u => u.LineUserId == lineUserId, cancellationToken);
  }

  public Task AddAsync(User user, CancellationToken cancellationToken = default)
  {
    return _dbContext.Users.AddAsync(user, cancellationToken).AsTask();
  }
}
