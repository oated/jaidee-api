namespace JaiDee.API.Line;

public interface ILineMessagingClient
{
  Task ReplyTextAsync(string replyToken, string message, CancellationToken cancellationToken = default);
  Task ReplyTextWithQuickRepliesAsync(string replyToken, string message, IReadOnlyList<string> quickReplies, CancellationToken cancellationToken = default);
  Task ReplyFlexAsync(string replyToken, string altText, object contents, CancellationToken cancellationToken = default);
}
