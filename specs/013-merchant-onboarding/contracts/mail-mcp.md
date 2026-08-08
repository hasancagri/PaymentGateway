# Contract — Mail.Mcp + Excel.Mcp + Common IMailSender

Generic altyapı MCP server'ları (BC değil). Domain bilmez. Çağıran BC içerik/şablonu üretir.

## Mail.Mcp — `src/others/Mail.Mcp`

Generic mail relay. `AddMcpServer().WithToolsFromAssembly()` + `MapMcp("/mcp")
.RequireAuthorization("mail.send")`. SMTP config'ten (`System.Net.Mail.SmtpClient`). Dev =
Mailpit (`:1025`), gerçek = Gmail SMTP. **Kalıcılık YOK.**

### Tool: `send_email`
```json
{
  "to": "merchant@example.com",
  "subject": "...",
  "body": "...",
  "isHtml": true,
  "attachments": [ { "fileName": "komisyon.xlsx", "contentBase64": "...", "contentType": "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } ]
}
```
- `attachments` opsiyonel (Excel eki için).
- Sonuç: `{ sent: true }` veya SMTP hata (çağıran BC FR-019 kaydına yansıtır).

## Excel.Mcp — `src/others/Excel.Mcp`

Generic spreadsheet üretici. Kütüphane **ClosedXML** (CPM'e ekle). `MapMcp("/mcp")` +
policy (öneri ayrı `document.generate` scope; tek çağıran olduğundan `mail.send` de kabul).

### Tool: `generate_spreadsheet`
```json
{
  "sheetName": "Komisyon",
  "columns": ["Kart Markası", "Kart Tipi", "Bölge", "Taksit", "Oran (%)"],
  "rows": [ ["Visa", "Kredi", "Yurtiçi", "3", "2.15"], ... ]
}
```
- Sonuç: `{ fileName, contentBase64, contentType }` (.xlsx).
- Domain bilmez — Commission.Api grid'i satır/sütun olarak verir.

## Common IMailSender — `src/others/Common/Mail` (yalnız DETERMİNİSTİK mailler)

Paylaşılan MCP client soyutlaması. BC inject eder (marker DI). MCP detayını gizler. **Yalnız
deterministik mailler** için (D14): aktivasyon (tek-seferlik MerchantKey linki) + admin "yeni
başvuru" bildirimi. Komisyon Excel maili bunu KULLANMAZ (harici LLM sürer).

```csharp
public interface IMailSender
{
    Task<FeatureResultModel> SendAsync(
        string to, string subject, string body,
        IReadOnlyList<MailAttachment>? attachments = null,
        CancellationToken ct = default);
}
```
- İç: Mail.Mcp `/mcp`'ye MCP client (Payment.Agent `McpToolProvider` deseni) + `mail.send`
  token'lı `DelegatingHandler` (AgentTokenHandler deseni, −30sn yenileme).
- FR-019: çağıran BC sonucu OnboardingNotification kaydına yazar (Sent/Failed + retry).

## Auth (plan A)

- Yeni scope `mail.send` (Identity `Config.cs`, resource `mail.mcp`).
- Mail atan BC başına Identity client: `merchant-api` (`mail.send`'li — deterministik mailler).
- Excel.Mcp scope: `document.generate` (öneri) veya `mail.send`.
- Harici LLM/MCP client (komisyon Excel'i) `mail.send` + `document.generate` + BC read
  scope'larını admin-düzlemi token'la taşır.

## Kompozisyon — iki ayrı yol

**A) Deterministik (BC handler → IMailSender)** — aktivasyon / admin bildirim:
```
Merchant.Api handler → IMailSender.SendAsync(to, subject, body)
                     → Mail.Mcp.send_email → SMTP → Mailpit/Gmail
                     → OnboardingNotification (Sent/Failed, FR-019)
```

**B) Agentik (harici LLM/MCP client → MCP tool zinciri)** — komisyon Excel'i (D14):
```
Harici LLM/MCP client (araç seçimi 013 dışı): "komisyon excelini oluştur ve mail at"
  LLM → Merchant.Api.get_merchant → Commission.Api.get_merchant_commission_grid
      → Excel.Mcp.generate_spreadsheet → Mail.Mcp.send_email(contactEmail, ..., [xlsx])
```
B'de IMailSender/BC handler YOK; LLM Mail.Mcp'yi doğrudan çağırır. 013 yalnız MCP yüzeylerini
sağlar; orkestratör client 013'te sabitlenmez (tool-bazında doğrulanır).