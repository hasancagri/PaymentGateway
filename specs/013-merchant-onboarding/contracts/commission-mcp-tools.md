# Contract — Commission.Api MCP tool yüzeyi (`/mcp`)

YENİ MCP yüzeyi (Payment.Api `/mcp` deseni). `AddMcpServer().WithHttpTransport(Stateless=true)
.WithToolsFromAssembly()` + `MapMcp("/mcp").RequireAuthorization("commission.read")`. Tool
yalnız `IMessageBus.InvokeAsync` ile slice sarar (`Features/Agent/**` veya read query).

Komisyon Excel orkestrasyonunun (D14) grid kaynağı. **Tüketen = harici LLM/MCP client**
(client seçimi 013 dışı), admin-düzlemi token.

## Tool: `get_merchant_commission_grid` (read-only)

- **Girdi**: `{ "merchantId": "..." }`
- **İç**: mevcut `GetMerchantCommissions` query'sini sarar (merchant grid'i, satır bazlı).
- **Taksit kapsamı**: grid satırları **bankanın desteklediği tüm taksit sayılarını** kapsar
  (`Bank.SupportedInstallments` — kombinasyon-bazlı grid davranışı). Sabit/kısıtlı taksit
  seti YOK; ne kadar taksit girilebiliyorsa o kadar satır döner. Eksik hücreler mevcut
  `IsMissing` bayrağıyla işaretli gelir.
- **Sonuç** (LLM'in Excel'e çevirebileceği düz tablo):
```json
{
  "merchantId": "...",
  "columns": ["Kart Markası", "Kart Tipi", "Bölge", "Taksit", "Oran (%)"],
  "rows": [
    ["Visa", "Kredi", "Yurtiçi", "1", "0.00"],
    ["Visa", "Kredi", "Yurtiçi", "2", "1.80"],
    ["Visa", "Kredi", "Yurtiçi", "3", "2.15"],
    ["...", "...", "...", "...(bankanın desteklediği azami taksite kadar)", "..."]
  ],
  "isEmpty": false
}
```
- **Statü alanı**: `{ ..., "status": "Draft" | "Ready" }`. Ready değilse (Draft/tanımsız) →
  `{ isEmpty: true, status: "Draft", rows: [] }` (LLM "hazır değil" der; Excel üretmez).
  Excel yalnız **Ready** grid'den.

## KAPSAM DIŞI (013 → 014)

- Kabul/ret/karşı-teklif tool'ları YOK (B kararı; komisyon gateway-otoriter). Pazarlık 014.

## Auth

`/mcp` policy `commission.read`. Çağıran harici LLM/MCP client (admin-düzlemi token,
merchant_id claim'siz). Grid = admin görünürlüğü; merchant kendi token'ıyla bu yüzeye girmez.

## Not — grid-hazır event ayrı (deterministik)

Bu read tool **agentik** (LLM sürer). Active koşulu #2 "grid hazır" ise **deterministik**:
admin grid'i **finalize** edip **Ready** yapınca `MerchantCommissionGridReady` yayınlar (outbox,
research D13). İkisi bağımsız — komisyon Excel'i hiç atılmasa bile koşul event'i gider.