using Common.Dependencies;

namespace Common.Mail;

/// <summary>
/// Paylaşılan mail gönderim soyutlaması (013, D7). BC inject eder (marker DI); MCP detayını görmez.
/// <b>Yalnız DETERMİNİSTİK mailler</b> için: aktivasyon (tek-seferlik MerchantKey linki) + admin
/// "yeni başvuru" bildirimi. Komisyon Excel maili bunu KULLANMAZ (harici LLM/MCP orkestrasyon).
/// İç: Mail.Mcp <c>/mcp</c> <c>send_email</c> tool'unu <c>mail.send</c> token'lı çağırır.
/// </summary>
public interface IMailSender
{
    Task<FeatureResultModel> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = false,
        IReadOnlyList<MailAttachment>? attachments = null,
        CancellationToken ct = default);
}

/// <summary>Mail eki (ör. komisyon .xlsx). İçerik base64 taşınır (Mail.Mcp sözleşmesi).</summary>
public sealed record MailAttachment(string FileName, string ContentBase64, string ContentType);