using JaiDee.Application.Interfaces.Repositories;
using JaiDee.Domain.Entities;
using JaiDee.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JaiDee.Infrastructure.Repositories;

public class ConversationStateRepository : IConversationStateRepository
{
  private readonly AppDbContext _dbContext;

  public ConversationStateRepository(AppDbContext dbContext)
  {
    _dbContext = dbContext;
  }

  public Task<ConversationState?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default)
  {
    return _dbContext.ConversationStates
      .FirstOrDefaultAsync(x => x.LineUserId == lineUserId, cancellationToken);
  }

  public Task AddAsync(ConversationState state, CancellationToken cancellationToken = default)
  {
    return _dbContext.ConversationStates.AddAsync(state, cancellationToken).AsTask();
  }

  public void Update(ConversationState state)
  {
    _dbContext.ConversationStates.Update(state);
  }

  public void Remove(ConversationState state)
  {
    _dbContext.ConversationStates.Remove(state);
  }

  public Task SaveChangesAsync(CancellationToken cancellationToken = default)
  {
    return _dbContext.SaveChangesAsync(cancellationToken);
  }
}
