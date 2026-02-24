using JaiDee.Domain.Common;
using JaiDee.Domain.Enums;

namespace JaiDee.Domain.Entities;

public class ConversationState : BaseEntity
{
  public string LineUserId { get; set; } = string.Empty;
  public ConversationStep Step { get; set; } = ConversationStep.None;
  public TransactionType? PendingType { get; set; }
  public string PendingNote { get; set; } = string.Empty;
  public decimal? PendingAmount { get; set; }
  public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow;
}
