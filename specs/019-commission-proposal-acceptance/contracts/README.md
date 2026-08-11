# Contracts: 019 Komisyon Teklifi ve Metin-Sürümlü Pazarlık

## 1. Commission.Api `/mcp` (YENİ yüzey — policy `commission.write`)

MCP tool'ları yalnız Agent slice'larını sarar (015 kuralları). Tüketici: Merchant.Agent.

### `submit_commission_proposal`

Teklif sunar / yeniden gönderir ("merchant'a gönder" komutunun da hedefi).

```jsonc
// input
{ "merchantId": "guid", "merchantEmail": "info@kahvedunyasi.com" }
// output (başarı)
{ "proposalId": "guid", "status": "Pending", "rowCount": 84, "mailQueued": true }
// hata örnekleri: banka grid'i boş | e-posta boş | Accepted teklif var (değişmezlik)
```

Davranış: draft yoksa banka grid + marjdan üretir; varsa mevcut draft fotoğraflanır. Önceki
Pending teklif Superseded olur. `SendEmailRequested` (tablo ekli) outbox'tan publish edilir.

### `revise_commission_draft`

```jsonc
// input — operations: LLM yalnız admin'in AÇIK değerlerini koyar
{ "merchantId": "guid", "operations": [
    { "op": "set",   "row": 37, "rate": 1.85 },
    { "op": "set",   "bank": "Akbank", "installment": 6, "rate": 1.8 },
    { "op": "delta", "filter": { "installment": 12 }, "delta": -0.2 },
    { "op": "set",   "filter": { "bank": "Akbank" }, "rate": 1.9 }
] }
// output (başarı) — diff yankısı
{ "changes": [ { "rowNo": 37, "bank": "Garanti", "installment": 9, "oldRate": 2.05, "newRate": 1.85 } ] }
// hata: taban ihlali (ihlal satırları listelenir, HİÇBİR değişiklik uygulanmaz) |
//       geçersiz satır/kombinasyon | kilitli (Accepted)
```

### `show_commission_draft`

```jsonc
// input
{ "merchantId": "guid" }
// output
{ "rows": [ { "rowNo": 1, "bank": "Akbank", "installment": 1, "rate": 1.79 } ], "isLocked": false }
```

### `commission_proposal_status`

```jsonc
// input
{ "merchantId": "guid" }
// output
{ "status": "Rejected", "decidedTime": "2026-08-11T14:05:00Z",
  "rejectReason": "6 ve 9 taksit oranları yüksek; tek çekim kabul.", "proposalId": "guid" }
// teklif hiç yoksa: { "status": "None" }
```

## 2. Karar uçları (Commission.Api, ANONİM — yetki = bilet)

| Metot | Yol | Amaç |
|-------|-----|------|
| GET  | `/commission-proposals/decision/{ticket}/accept` | Mini HTML onay sayfası ("Kabul ediyorum" butonu) |
| POST | `/commission-proposals/decision/{ticket}/accept` | Kabul icrası → sonuç sayfası |
| GET  | `/commission-proposals/decision/{ticket}/reject` | Gerekçe formu |
| POST | `/commission-proposals/decision/{ticket}/reject` | Ret icrası (form alanı `reason` zorunlu) → sonuç sayfası |

- Bilet kuralları: tek kullanım + TTL + yalnız Pending (Superseded/karar-almış → "geçersiz bilet" sayfası, durum değişmez).
- Kabul POST'u aynı `[Transactional]` içinde: proposal Accept + draft Lock + satırlar
  `MerchantCommission`'a kopya + `MerchantCommissionGridReady` publish (mevcut kontrat, DEĞİŞMEZ).
- Mutlak link tabanı `CommissionProposalOption.PublicBaseUrl`'den.

## 3. Mesaj kontratı (Shared — Mail.Worker tüketir)

```csharp
public record EmailAttachmentTable(string FileName, string[] Headers, string[][] Rows);
public record SendEmailRequested(string To, string Subject, string Body,
    bool IsHtml = false, EmailAttachmentTable? Attachment = null);
```

Mail.Worker: `Attachment != null` → ClosedXML ile xlsx üret (Headers + Rows; RowNo dahil ilk
kolon), `Attachment` olarak ekle. Retry/dead-letter davranışı değişmez.

## 4. Merchant.Agent skill'leri (A2A yüzeyi)

| Skill | Tool zinciri (LLM yalnız sıra kurar) |
|-------|--------------------------------------|
| `propose_commission` | `get_merchant` (isim→id+email, Merchant /mcp) → `submit_commission_proposal` |
| `revise_commission_draft` | (gerekirse `get_merchant`) → `revise_commission_draft` → diff'i yankıla |
| `show_commission_draft` | `show_commission_draft` → satır no'lu tablo |
| `commission_proposal_status` | `commission_proposal_status` |

Identity: `merchant-agent` client'ına `commission.write` scope eklenir (seed güncellemesi).

## 5. Kaldırılan kontratlar

- `POST merchants/{merchantId}/commissions/finalize` → SİLİNİR.
- Admin UI merchant komisyon upsert uçları → salt-okuma kalır (grid + teklif durumu).