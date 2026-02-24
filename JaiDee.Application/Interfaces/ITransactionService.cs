using JaiDee.Application.DTOs;

namespace JaiDee.Application.Interfaces;

public interface ITransactionService
{
  Task<RecordTransactionResult> RecordTransactionAsync(RecordTransactionCommand command, CancellationToken cancellationToken = default);
  Task<MonthlySummaryDto?> GetMonthlySummaryAsync(string lineUserId, int year, int month, CancellationToken cancellationToken = default);
}
