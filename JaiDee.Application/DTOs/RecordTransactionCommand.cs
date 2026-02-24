using JaiDee.Domain.Enums;

namespace JaiDee.Application.DTOs;

public class RecordTransactionCommand
{
  public string LineUserId { get; set; } = string.Empty;
  public string DisplayName { get; set; } = string.Empty;
  public TransactionType Type { get; set; }
  public decimal Amount { get; set; }
  public string Note { get; set; } = string.Empty;
  public DateTime? TransactionDate { get; set; }
}
