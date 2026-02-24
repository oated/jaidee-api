namespace JaiDee.API.Line;

public class LineBotOptions
{
  public const string SectionName = "LineBot";

  public string ChannelSecret { get; set; } = string.Empty;
  public string ChannelAccessToken { get; set; } = string.Empty;
  public string ApiBaseUrl { get; set; } = "https://api.line.me/";
  public bool SkipSignatureValidation { get; set; }
  public int NoteQuickReplyCount { get; set; } = 3;
  public Dictionary<string, string> QuickReplyIcons { get; set; } = new();
  public List<string> ExpenseNoteSuggestions { get; set; } = new()
  {
    "ค่าข้าว",
    "น้ำหวาน",
    "กาแฟ",
    "เดินทาง",
    "ของใช้"
  };
  public List<string> IncomeNoteSuggestions { get; set; } = new()
  {
    "เงินเดือน",
    "พิเศษ",
    "โบนัส",
    "คืนเงิน"
  };
}
