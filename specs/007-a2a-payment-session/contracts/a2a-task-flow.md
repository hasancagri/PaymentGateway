# A2A Task Yaşam Döngüsü ↔ PaymentSession (007)

İki fazlı akış (quote → seçim) A2A task lifecycle'ına oturur. `A2AAgentHandler` (Microsoft Agent
Framework A2A köprüsü) task durumlarını `TaskUpdater` ile sürer; `contextId` → `PaymentSession`.

## Eşleme

| A2A task durumu | Tetik | PaymentSession fazı | Agent aksiyonu |
|-----------------|-------|---------------------|----------------|
| `submitted` | E-ticaret agent task yollar (niyet + token + cartAmount) | — | Handler task'ı alır |
| `working` | LLM `get_installment_options` çağırır | `Opened` → `QuoteProvided` | MCP tool → Model A liste |
| `input-required` | Taksit listesi kullanıcıya sunulur, seçim beklenir | `QuoteProvided` | Task duraklar; `contextId` korunur |
| `working` (devam) | Kullanıcı taksit seçer → agent `select_installment` çağırır | `QuoteProvided` → `InstallmentSelected` | MCP tool → seçimi yaz |
| `completed` | Seçim yazıldı (007 terminal) | `InstallmentSelected` | Artifact: seçilen taksit + sessionId |
| `failed` | Geçersiz token / POS yok / tutar ≤ 0 / boş liste | `Failed` | Hata mesajı (kart verisi sızmaz) |

> **007'de `completed` = "taksit seçildi"** — fiili çekim değildir. Çekim (ve olası `Awaiting3D`
> ara durumu) pay feature'ında bu oturumun devamı olarak gelir.

## Akış (mermaid)

```
E-ticaret agent          Payment.Agent (LLM)        Payment.Api (MCP + domain)
     │  A2A task (submitted)   │                             │
     │────────────────────────►│                             │
     │                         │ get_installment_options     │
     │                         │────────────────────────────►│  token→BIN, BankRouter, Model A
     │   input-required        │◄────────────────────────────│  sessionId + liste (QuoteProvided)
     │◄────────────────────────│                             │
     │  (kullanıcı taksit seçer)│                             │
     │  seçim ────────────────►│ select_installment          │
     │                         │────────────────────────────►│  seçim ⊂ sunulanlar → yaz
     │   completed             │◄────────────────────────────│  InstallmentSelected
     │◄────────────────────────│                             │
     │                         │  (payment_status ile sorgulanabilir — Story 3)
```

## Session isolation (güvenlik notu)

A2A köprüsü varsayılan `contextId`'yi tek session anahtarı yapar. Yetki 007'de ertelenmiş
olsa da, çok-tenant sızıntıyı önlemek için ileride `SessionIsolationKeyProvider` (claims-tabanlı)
takılmalı. 007'de tek güvenlik garantisi: kart verisi kanala girmez + token sahibi doğrulaması
`ICardVault`'a (008) devredilir.