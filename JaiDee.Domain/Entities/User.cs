using JaiDee.Domain.Common;

namespace JaiDee.Domain.Entities;

public class User : BaseEntity
{
  public string LineUserId { get; set; } = string.Empty;
  public string DisplayName { get; set; } = string.Empty;

  public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}