using JaiDee.Domain.Common;
using JaiDee.Domain.Enums;

namespace JaiDee.Domain.Entities;

public class Transaction : BaseEntity
{
  public Guid UserId { get; set; }
  public TransactionType Type { get; set; }

  public decimal Amount { get; set; }
  public string Note { get; set; } = string.Empty;

  public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

  public User? User { get; set; }
}