namespace JaiDee.Application.DTOs;

public class RecordTransactionResult
{
  public Guid TransactionId { get; set; }
  public decimal MonthlyIncome { get; set; }
  public decimal MonthlyExpense { get; set; }
}
