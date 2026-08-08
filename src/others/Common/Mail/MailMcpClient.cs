using System.Text.Json;
using Common.Dependencies;
using Common.Utils.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Common.Mail;

/// <summary>
/// <see cref="IMailSender"/>'ın Mail.Mcp <c>/mcp</c> üstünden gerçeklemesi (013 D7). MCP client'ı
/// (StreamableHttp + <see cref="MailMcpTokenHandler"/>) lazy kurar ve app-ömrü boyunca canlı tutar.
/// Yalnız deterministik mailler (aktivasyon + admin bildirim) buradan gider.
/// </summary>
public sealed class MailMcpClient(IConfiguration config, ILogger<MailMcpClient> logger)
    : IMailSender, ISingletonDependency
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private McpClient? _client;

    public async Task<FeatureResultModel> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = false,
        IReadOnlyList<MailAttachment>? attachments = null,
        CancellationToken ct = default)
    {
        try
        {
            var client = await GetClientAsync(ct);

            var args = new Dictionary<string, object?>
            {
                ["to"] = to,
                ["subject"] = subject,
                ["body"] = body,
                ["isHtml"] = isHtml
            };

            if (attachments is { Count: > 0 })
                args["attachments"] = attachments
                    .Select(a => new { fileName = a.FileName, contentBase64 = a.ContentBase64, contentType = a.ContentType })
                    .ToArray();

            var result = await client.CallToolAsync("send_email", args, cancellationToken: ct);

            if (result.IsError == true || !ParseSent(result))
            {
                logger.LogWarning("Mail.Mcp send_email başarısız (SMTP): {To}", to);
                return FeatureResultModel.Error(new MessageItem
                {
                    Code = CommonResourceConstants.COMMON_MESSAGE_SERVER_ERROR
                });
            }

            return FeatureResultModel.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Mail.Mcp çağrısı başarısız: {To}", to);
            return FeatureResultModel.Error(new MessageItem
            {
                Code = CommonResourceConstants.COMMON_MESSAGE_SERVER_ERROR
            });
        }
    }

    // send_email sonucu {sent, error} — text content bloğundaki JSON'dan okunur (SMTP hatası IsError=false).
    private static bool ParseSent(CallToolResult result)
    {
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
            return true; // içerik yoksa hata sayma (IsError zaten kontrol edildi)

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("sent", out var sent))
                return sent.GetBoolean();
        }
        catch (JsonException)
        {
            // JSON değilse (beklenmedik biçim) hata sayma; IsError zaten değerlendirildi.
        }

        return true;
    }

    private async Task<McpClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is not null)
            return _client;

        await _gate.WaitAsync(ct);
        try
        {
            if (_client is not null)
                return _client;

            var baseUrl = config["services:mail-mcp:http:0"]
                          ?? config["MailMcp:BaseUrl"]
                          ?? "http://mail-mcp";
            var endpoint = $"{baseUrl.TrimEnd('/')}/mcp";

            var http = new HttpClient(new MailMcpTokenHandler(config) { InnerHandler = new HttpClientHandler() });

            _client = await McpClient.CreateAsync(
                new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Name = "mail-mcp",
                        Endpoint = new Uri(endpoint),
                        TransportMode = HttpTransportMode.StreamableHttp
                    },
                    http,
                    ownsHttpClient: true),
                cancellationToken: ct);

            return _client;
        }
        finally
        {
            _gate.Release();
        }
    }
}