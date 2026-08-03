# Quickstart — A2A Ödeme Oturumu (007) Doğrulama

Elle doğrulama rehberi (anayasa: A2A/MCP/LLM entegrasyonu birim testi edilmez, quickstart ile
doğrulanır). Kapsam: quote + taksit seçimi + durum. **Çekim yok.**

## Önkoşullar

- .NET 10 SDK, Docker (Aspire Postgres/RabbitMQ için)
- Chat model erişimi: dev'de GitHub Models token (`GITHUB_TOKEN`) veya Azure OpenAI. AppHost'ta
  `chat` resource olarak tanımlı; `payment-agent` bunu tüketir.
- PosAccount'lar seed edilmiş olmalı (taksit gridleri) — yoksa quote boş döner. `pos-accounts`
  endpoint'lerinden en az bir aktif POS + komisyon gridi oluştur.

## Çalıştırma

```bash
dotnet build                                              # tüm çözüm
dotnet run --project src/aspire/AppHost/AppHost.csproj    # Postgres + RabbitMQ + api + agent
dotnet test tests/Payment.Api.Tests                       # saf domain birim testleri (PaymentSession)
```

Aspire dashboard'da `payment-api`, `payment-agent`, `postgres`, `rabbitmq` çalışır durumda olmalı.

## Sağlık kontrolleri

- Agent Card: `GET http://<payment-agent>/.well-known/agent-card.json` → 3 skill
  (`quote-installments`, `select-installment`, `payment-status`). `pay-with-token` **yok**.
- MCP server: `payment-api`'nin MCP endpoint'i (`/mcp`) tool listesi → `get_installment_options`,
  `select_installment`, `payment_status`.

## Senaryolar (spec acceptance eşlemesi)

### S1 — Quote: taksit listesi (Story 1)

1. Payment.Agent'a A2A task: niyet metni + geçerli `cardToken` (kredi kartı) + `cartAmount`.
2. **Beklenen**: task `input-required`; dönen listede yalnız desteklenen taksitler; her satır
   `userTotalAmount == cartAmount` (Model A, SC-002); `monthlyAmount == cartAmount / n`.
3. Doğrula (Story 1 AC-1..4): 6 taksiti destekleyen POS yoksa 6 listede **yok** (SC-003);
   banka kartı token'ı → yalnız peşin (AC-3); tutar şişmiyor (AC-4).

### S2 — Seçim (Story 2)

1. S1'in `sessionId`'siyle bir taksit seç (`select_installment`).
2. **Beklenen**: task `completed`; `status = InstallmentSelected`; `selectedInstallmentCount`
   yazıldı. **Çekim tetiklenmez**, `Payment` kaydı oluşmaz.
3. Negatif: sunulmayan bir `installmentCount` → reddedilir (AC-2). Quote yapılmamış `sessionId`
   → reddedilir (FR-017).

### S3 — Durum (Story 3)

1. `payment_status(sessionId)` → güncel faz.
2. **Beklenen**: her faz geçişinden sonra doğru durum (`QuoteProvided` → `InstallmentSelected`).

### S4 — Güvenlik sınırı (SC-006)

1. Agent Card şemasında ve MCP tool input'larında tam PAN/CVV/expiry alanı **olmadığını** doğrula.
2. Geçersiz/süresi dolmuş token ile quote → anlamlı hata, kart verisi sızmadan (FR-019).

## Birim test kapsamı (tests/Payment.Api.Tests)

- `PaymentSession`: `Create` (tutar ≤ 0 reddi), `OfferInstallments` (boş liste → Failed,
  Model A satır invariant'ı), `SelectInstallment` (⊂ sunulanlar, quote'suz reddi, tekrar seçim),
  faz geçiş sırası.
- Model A tutar hesabı: `userTotalAmount == cartAmount`, `monthlyAmount` yuvarlama (sapma 0).

## Kapsam dışı (pay feature'ında doğrulanacak)

Fiili çekim, failover, 3D yönlendirme, `Payment` kaydı, çekilen tutar = sepet tutarı (SC-005).
007 bunları **yapmaz**; seçilen taksiti seam'e bırakır.