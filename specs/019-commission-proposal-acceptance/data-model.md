# Data Model: Komisyon Teklifi ve Metin-Sürümlü Pazarlık

**Feature**: 019 | **Date**: 2026-08-11 | Commission BC (Marten dökümanları)

## CommissionDraft (YENİ aggregate — `Domains/CommissionDrafts/`)

Merchant başına TEK çalışma kopyası. `Id = MerchantId` (birebir; `MerchantCommissionGrid`
deseninin devamı).

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | = MerchantId |
| Rows | List\<DraftRow\> | private tutulur, salt-okuma expose |
| IsLocked | bool | kabul sonrası true — hiçbir revizyon kabul edilmez |

**DraftRow (VO)**: `RowNo:int` (1-tabanlı, deterministik: BankCode ASC → Installment ASC),
`BankCode:string`, `BankName:string`, `Installment:int`, `Rate:decimal`.

**Davranışlar** (hepsi `ResultDomain`/`ResultDomain<T>`, handler-çağrılı):

- `CreateFromBankGrid(merchantId, bankRows, marginPoints)` → `ResultDomain<CommissionDraft>`:
  banka satırlarından oran+marj ile satırları üretir, sıralar, numaralandırır. Boş banka grid'i →
  Error (RECORD_NOT_FOUND).
- `Revise(operations, bankFloorLookup)` → `ResultDomain<List<DraftChange>>`: set/delta işlemlerini
  uygular. Sonuç-oran ilgili banka oranının ALTINDAYSA işlem BÜTÜN reddedilir (taban bekçisi);
  geçersiz satır no / bilinmeyen kombinasyon / kilitli draft → Error. Başarıda diff listesi döner
  (`DraftChange: RowNo, BankName, Installment, OldRate, NewRate`).
- `Lock()` → `ResultDomain`: kabul anında kilitler (idempotent).

## CommissionProposal (YENİ aggregate — `Domains/CommissionProposals/`)

Gönderilmiş taslak fotoğrafı. Merchant başına yalnız BİRİ karar alabilir (Pending son teklif).

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | teklif kimliği (her gönderim yeni kayıt) |
| MerchantId | Guid | |
| Rows | List\<DraftRow\> | gönderim anı fotoğrafı (immutable) |
| Status | ProposalStatus | Pending / Accepted / Rejected / Superseded |
| DecisionTicket | string | tek-kullanımlık jeton (`cp_` + Guid "N") |
| TicketExpiresAt | DateTime | UtcNow + TicketTtlHours |
| DecidedTime | DateTime? | kabul/ret zamanı |
| RejectReason | string? | ret gerekçesi (serbest metin, uzun olabilir) |

**ProposalStatus (düz enum)**: `Pending=1, Accepted=2, Rejected=3, Superseded=4`

**Davranışlar**:

- `IssueFrom(draft, ttlHours)` → `ResultDomain<CommissionProposal>`: draft fotoğrafından Pending
  teklif + yeni bilet üretir (fabrika, Ok sarılı — 014 sözleşmesi).
- `Supersede()` → `ResultDomain`: Pending → Superseded (yeni gönderimde eski teklife uygulanır;
  idempotent).
- `Accept(now)` → `ResultDomain`: bilet geçerliyse Pending → Accepted + DecidedTime. Kullanılmış/
  süresi dolmuş/Superseded → Error (durum değişmez).
- `Reject(reason, now)` → `ResultDomain`: Pending → Rejected + gerekçe + DecidedTime. Aynı bilet
  kuralları.

**Durum geçişleri**:

```text
        IssueFrom               Accept (bilet OK)
  (yok) ────────▶ Pending ───────────────▶ Accepted   [terminal — değişmezlik]
                    │  ▲
                    │  └── yeni gönderim: önceki Pending ──▶ Superseded [terminal]
                    └── Reject (bilet OK, gerekçe) ──▶ Rejected [admin revize → yeni gönderim]
```

**Değişmezlik kuralı (FR-012)**: MerchantId'ye ait Accepted teklif varken yeni teklif ve draft
revizyonu RET döner (handler sorgusu + `IsLocked` çifte bariyer).

## Değişen mevcut modeller

- **MerchantCommissionGrid + GridStatus**: SİLİNİR (FR-013, R6). "Hazır" kavramının tek kaynağı
  Accepted proposal.
- **MerchantCommission**: yapı değişmez; artık YALNIZ kabul handler'ı tarafından (draft
  satırlarından kopya) yazılır. Admin UI upsert uçları kalkar/salt-okumaya döner.
- **Shared.IntegrationEvents**: `SendEmailRequested`'e opsiyonel ek:
  `EmailAttachmentTable(FileName:string, Headers:string[], Rows:string[][])`;
  `SendEmailRequested(To, Subject, Body, IsHtml, Attachment?)`. `MerchantCommissionGridReady`
  DEĞİŞMEZ (mevcut aktivasyon zinciri korunur).