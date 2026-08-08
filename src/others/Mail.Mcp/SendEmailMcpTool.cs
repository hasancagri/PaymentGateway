using System.ComponentModel;
using System.Net;
using System.Net.Mail;
using ModelContextProtocol.Server;

namespace Mail.Mcp;

/// <summary>
/// Generic mail gönderim tool'u. Domain bilmez — çağıran (BC handler ya da harici LLM) içerik/şablonu
/// üretir. SMTP config'ten (<c>Mail:Smtp:*</c>); dev = Mailpit (host <c>mailpit</c>, port 1025, auth yok),
/// gerçek = Gmail SMTP. Kalıcılık YOK. Ekler (Excel gibi) base64 olarak taşınır.
/// </summary>
[McpServerToolType]
public static class SendEmailMcpTool
{
    /// <summary>MCP eki taşıma sözleşmesi (contracts/mail-mcp.md).</summary>
    public record EmailAttachment(string FileName, string ContentBase64, string ContentType);

    public class SendEmailResult
    {
        public bool Sent { get; set; }
        public string? Error { get; set; }
    }

    [McpServerTool(Name = "send_email")]
    [Description("Belirtilen alıcıya e-posta gönderir (SMTP). Opsiyonel base64 ekler taşınabilir " +
                 "(ör. .xlsx). Domain bilmez; konu/gövde çağıran tarafından verilir. Dev'de tüm mailler " +
                 "Mailpit'e düşer.")]
    public static async Task<SendEmailResult> SendEmailAsync(
        [Description("Alıcı e-posta adresi")] string to,
        [Description("Konu")] string subject,
        [Description("Gövde (düz metin veya HTML)")] string body,
        [Description("Gövde HTML mi?")] bool isHtml,
        IConfiguration config,
        CancellationToken ct,
        [Description("Opsiyonel ekler (fileName + base64 içerik + contentType)")]
        EmailAttachment[]? attachments = null)
    {
        var host = config["Mail:Smtp:Host"] ?? "mailpit";
        var port = int.TryParse(config["Mail:Smtp:Port"], out var p) ? p : 1025;
        var from = config["Mail:Smtp:From"] ?? "onboarding@dropshop.local";
        var user = config["Mail:Smtp:User"];
        var password = config["Mail:Smtp:Password"];

        try
        {
            using var message = new MailMessage(from, to, subject, body) { IsBodyHtml = isHtml };

            if (attachments is not null)
            {
                foreach (var a in attachments)
                {
                    var bytes = Convert.FromBase64String(a.ContentBase64);
                    var stream = new MemoryStream(bytes);
                    var attachment = new Attachment(stream, a.FileName, a.ContentType);
                    message.Attachments.Add(attachment);
                }
            }

            using var smtp = new SmtpClient(host, port);
            if (!string.IsNullOrWhiteSpace(user))
            {
                smtp.Credentials = new NetworkCredential(user, password);
                smtp.EnableSsl = bool.TryParse(config["Mail:Smtp:EnableSsl"], out var ssl) && ssl;
            }

            await smtp.SendMailAsync(message, ct);
            return new SendEmailResult { Sent = true };
        }
        catch (Exception ex)
        {
            // SMTP hatası çağırana taşınır (BC FR-019 kaydına yansıtır; harici LLM'in gözünde görünür).
            return new SendEmailResult { Sent = false, Error = ex.Message };
        }
    }
}