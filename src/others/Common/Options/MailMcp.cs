namespace Common.Options;

// Mail.Mcp /mcp taban adresi (Aspire service discovery yoksa fallback). Discovery anahtarı
// (services:mail-mcp:http:0) çözülemezse BaseUrl kullanılır.
public class MailMcp
{
    public string BaseUrl { get; set; } = "http://mail-mcp";
}
