namespace Mail.Mcp.Options;

// SMTP ayarları (section Mail:Smtp). Dev varsayılanı Mailpit (host mailpit, port 1025, auth yok).
public class Mail
{
    public Smtp Smtp { get; set; } = new();
}

public class Smtp
{
    public string Host { get; set; } = "mailpit";
    public int Port { get; set; } = 1025;
    public string From { get; set; } = "onboarding@dropshop.local";
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; }
}
