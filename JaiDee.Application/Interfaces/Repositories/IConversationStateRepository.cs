using JaiDee.Domain.Entities;

namespace JaiDee.Application.Interfaces.Repositories;

public interface IConversationStateRepository
{
  Task<ConversationState?> GetByLineUserIdAsync(string lineUserId, CancellationToken cancellationToken = default);
  Task AddAsync(ConversationState state, CancellationToken cancellationToken = default);
  void Update(ConversationState state);
  void Remove(ConversationState state);
  Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
