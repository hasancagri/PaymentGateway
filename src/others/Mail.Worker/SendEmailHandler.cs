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

        // 019: generic tablo eki → ClosedXML ile .xlsx üret (Headers + Rows; içerik domain-bağımsız).
        using var attachmentStream = new MemoryStream();
        if (msg.Attachment is { } table)
        {
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Tablo");
                for (var c = 0; c < table.Headers.Length; c++)
                {
                    sheet.Cell(1, c + 1).Value = table.Headers[c];
                    sheet.Cell(1, c + 1).Style.Font.Bold = true;
                }

                for (var r = 0; r < table.Rows.Length; r++)
                for (var c = 0; c < table.Rows[r].Length; c++)
                    sheet.Cell(r + 2, c + 1).Value = table.Rows[r][c];

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(attachmentStream);
            }

            attachmentStream.Position = 0;
            message.Attachments.Add(new Attachment(attachmentStream, table.FileName,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        }

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