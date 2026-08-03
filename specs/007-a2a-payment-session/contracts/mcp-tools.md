# MCP Tool Sözleşmesi — Payment.Api (007)

Payment.Api'nin MCP server'ı (`ModelContextProtocol.AspNetCore`, `MapMcp()`, Streamable HTTP)
Payment.Agent'a 3 tool sunar. Her tool `[McpServerTool]` + `[Description]`; gövdesi
`IMessageBus.InvokeAsync` ile ilgili slice'ı çağırır. **Tutar session'dan okunur** (LLM üretmez).

> `process_payment` tool'u 007'de **YOK** — çekim ertelendi; pay feature'ında eklenecek.

---

## `get_installment_options`

Faz 1 — token + sepet tutarından Model A taksit listesi üretir, oturumu açar.

**Input**

| Alan | Tip | Not |
|------|-----|-----|
| `cardToken` | string | Kayıtlı kart token'ı. PAN değil. |
| `cartAmount` | number (decimal) | Sepet tutarı, TL, > 0 |

**Output**

| Alan | Tip | Not |
|------|-----|-----|
| `sessionId` | guid | Yeni `PaymentSession` kimliği |
| `status` | string | `QuoteProvided` (veya boş liste ise `Failed`) |
| `installments` | array | `{ installmentCount, userTotalAmount, monthlyAmount }` — her `userTotalAmount == cartAmount` (Model A) |

**Hatalar (Result)**: geçersiz/yetkisiz token → reddedilir, kart verisi sızmadan (FR-019).
`cartAmount ≤ 0` → reddedilir. Hiç POS/peşin yoksa → `status = Failed` (FR: "ödeme alınamıyor").

**Sarar**: `Features/Agent/QuoteInstallmentsForSession`.

---

## `select_installment`

Faz 2 — kullanıcının seçtiği taksiti oturuma yazar. **Çekim yapmaz.**

**Input**

| Alan | Tip | Not |
|------|-----|-----|
| `sessionId` | guid | Açık oturum |
| `installmentCount` | int | Sunulan listeden biri |

**Output**

| Alan | Tip | Not |
|------|-----|-----|
| `sessionId` | guid | |
| `status` | string | `InstallmentSelected` |
| `selectedInstallmentCount` | int | Yazılan seçim |

**Hatalar (Result)**: `installmentCount` ⊄ sunulanlar → reddedilir (FR-012). Oturum
`QuoteProvided`/`InstallmentSelected` fazında değilse → reddedilir (FR-017).

**Sarar**: `Features/Agent/SelectInstallment`. *(Not: burada seçim seam'e devredilir; sonraki pay
feature'ı bu oturumu okur.)*

---

## `payment_status`

Oturumun güncel fazını döner (Story 3).

**Input**

| Alan | Tip | Not |
|------|-----|-----|
| `sessionId` | guid | |

**Output**

| Alan | Tip | Not |
|------|-----|-----|
| `sessionId` | guid | |
| `status` | string | `Opened` / `QuoteProvided` / `InstallmentSelected` / `Failed` |
| `selectedInstallmentCount` | int? | varsa |
| `failReason` | string? | `Failed` ise (kart verisi sızdırmaz) |

**Sarar**: `Features/Agent/GetPaymentSessionStatus`.

---

## Güvenlik sınırı

- Tool input'larında **tam kart alanı (PAN/CVV/expiry) YOK** — yalnız `cardToken` (SC-006).
- Token → kart-bilgisi çözümü Payment.Api içinde `ICardVault` ile (server-side), tool sınırının
  ötesinde. 007'de PAN'a hiç dokunulmaz.