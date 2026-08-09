namespace Mail.Worker;

/// <summary>
/// 016: deterministik mail teslim tüketicisi. BC handler'ı <c>SendEmailRequested</c> yayınlar (outbox),
/// burası SMTP ile gönderir. Domain bilmez — içerik çağıran tarafından üretilir. Dev = Mailpit.
/// SMTP hatası fırlatılır (yutulmaz) → Wolverine retry policy'si devreye girer (Program.cs).
/// </summary>
public static class SendEmailHandler
{
    public static async Task Handle(
        SendEmailRequested msg,
        Mail.Worker.Options.Mail mail,
        ILogger logger,
        CancellationToken ct)
    {
        using var message = new MailMessage(mail.Smtp.From, msg.To, msg.Subject, msg.Body)
        {
            IsBodyHtml = msg.IsHtml
        };

        using var smtp = new SmtpClient(mail.Smtp.Host, mail.Smtp.Port);
        if (!string.IsNullOrWhiteSpace(mail.Smtp.User))
        {
            smtp.Credentials = new NetworkCredential(mail.Smtp.User, mail.Smtp.Password);
            smtp.EnableSsl = mail.Smtp.EnableSsl;
        }

        // Hata fırlatılırsa yutulmaz — Wolverine SmtpException'ı retry/dead-letter ile ele alır.
        await smtp.SendMailAsync(message, ct);
        logger.LogInformation("Mail gönderildi: {To} ({Subject})", msg.To, msg.Subject);
    }
}