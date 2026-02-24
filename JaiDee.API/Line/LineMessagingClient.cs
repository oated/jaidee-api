using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace JaiDee.API.Line;

public class LineMessagingClient : ILineMessagingClient
{
  private readonly HttpClient _httpClient;
  private readonly LineBotOptions _options;

  public LineMessagingClient(HttpClient httpClient, IOptions<LineBotOptions> options)
  {
    _httpClient = httpClient;
    _options = options.Value;
  }

  public async Task ReplyTextAsync(string replyToken, string message, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(replyToken))
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(_options.ChannelAccessToken))
    {
      throw new InvalidOperationException("LineBot:ChannelAccessToken is not configured.");
    }

    await SendReplyAsync(
      replyToken,
      new
      {
        type = "text",
        text = message
      },
      cancellationToken);
  }

  public async Task ReplyTextWithQuickRepliesAsync(
    string replyToken,
    string message,
    IReadOnlyList<string> quickReplies,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(replyToken))
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(_options.ChannelAccessToken))
    {
      throw new InvalidOperationException("LineBot:ChannelAccessToken is not configured.");
    }

    var quickReplyItems = quickReplies
      .Where(x => !string.IsNullOrWhiteSpace(x))
      .Distinct(StringComparer.Ordinal)
      .Select(BuildQuickReplyItem)
      .ToArray();

    await SendReplyAsync(
      replyToken,
      new
      {
        type = "text",
        text = message,
        quickReply = new
        {
          items = quickReplyItems
        }
      },
      cancellationToken);
  }

  public async Task ReplyFlexAsync(string replyToken, string altText, object contents, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(replyToken))
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(_options.ChannelAccessToken))
    {
      throw new InvalidOperationException("LineBot:ChannelAccessToken is not configured.");
    }

    await SendReplyAsync(
      replyToken,
      new
      {
        type = "flex",
        altText,
        contents
      },
      cancellationToken);
  }

  private async Task SendReplyAsync(string replyToken, object message, CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(HttpMethod.Post, "v2/bot/message/reply");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);
    request.Content = JsonContent.Create(new
    {
      replyToken,
      messages = new[] { message }
    });

    using var response = await _httpClient.SendAsync(request, cancellationToken);
    if (response.IsSuccessStatusCode)
    {
      return;
    }

    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
    throw new InvalidOperationException($"LINE Reply API failed ({(int)response.StatusCode}): {errorBody}");
  }

  private object BuildQuickReplyItem(string label)
  {
    var iconUrl = ResolveQuickReplyIcon(label);

    if (string.IsNullOrWhiteSpace(iconUrl))
    {
      return new
      {
        type = "action",
        action = new
        {
          type = "message",
          label,
          text = label
        }
      };
    }

    return new
    {
      type = "action",
      imageUrl = iconUrl,
      action = new
      {
        type = "message",
        label,
        text = label
      }
    };
  }

  private string? ResolveQuickReplyIcon(string label)
  {
    var iconMap = _options.QuickReplyIcons;
    if (iconMap is null || iconMap.Count == 0)
    {
      return null;
    }

    if (iconMap.TryGetValue(label, out var exactMatch) && !string.IsNullOrWhiteSpace(exactMatch))
    {
      return exactMatch;
    }

    foreach (var pair in iconMap)
    {
      if (string.Equals(pair.Key, label, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
      {
        return pair.Value;
      }
    }

    return null;
  }
}
