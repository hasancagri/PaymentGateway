# Data Model: Merchant SubMerchant Model (023)

## Merchant (AggregateRoot) — `Domains/Merchants/Merchant.cs`

Pazaryeri satıcısı; iyzico SubMerchant sözleşmesiyle hizalı alan seti (R1). Marten
dokümanı olarak `merchant` şemasında saklanır. Private setter'lar + statik `Create`
fabrikası; tüm mutasyon davranış metotlarından geçer.

### Alanlar

| Alan | Tip | Kaynak/Kural |
|------|-----|--------------|
| `Id` | `Guid` | AggregateRoot; `Create`'te üretilir, değişmez |
| `MerchantKey` | `string` | `"mk_" + Guid` (R4); `Create`'te üretilir, DEĞİŞMEZ; sorgu yanıtlarında asla yok |
| `Status` | `MerchantStatus` | Varsayılan `Active` (R5) |
| `Type` | `MerchantType` | İşyeri tipi; zorunlu alan matrisini belirler (R2) |
| `Name` | `string` | Zorunlu (boş olamaz) |
| `Email` | `string` | Zorunlu + biçim denetimi (R3) |
| `GsmNumber` | `string` | Zorunlu (biçim denetimi yok — YAGNI) |
| `Address` | `string` | Zorunlu |
| `Iban` | `string` | Zorunlu + TR mod-97 denetimi (R3) |
| `ContactName` | `string` | Zorunlu |
| `ContactSurname` | `string` | Zorunlu |
| `IdentityNumber` | `string?` | Tip-koşullu (matris) |
| `TaxOffice` | `string?` | Tip-koşullu (matris) |
| `TaxNumber` | `string?` | Tip-koşullu (matris) |
| `LegalCompanyTitle` | `string?` | Tip-koşullu (matris) |
| `SubMerchantKey` | `string?` | Bu fazda HEP `null` — iyzico entegrasyonu (ayrı iş) doldurur; boşluğu hiçbir akışı engellemez |

### Tip-uyum matrisi (R2)

| `Type` | Zorunlu ek alanlar |
|--------|--------------------|
| `Personal` | `IdentityNumber` |
| `PrivateCompany` | `IdentityNumber`, `TaxOffice`, `LegalCompanyTitle` |
| `LimitedOrJointStockCompany` | `TaxOffice`, `TaxNumber`, `LegalCompanyTitle` |

Fazla dolu alan reddedilmez; yalnız zorunluluk denetlenir.

### Davranışlar (014 sonuç sözleşmesi — hepsi handler'dan çağrılır, 015)

| Metot | İmza | Kural | Handler |
|-------|------|-------|---------|
| `Create` | `static ResultDomain<Merchant> Create(type, name, email, gsm, address, iban, contactName, contactSurname, identityNumber, taxOffice, taxNumber, legalCompanyTitle)` | Zorunlu alanlar + e-posta/IBAN biçimi + tip-uyum matrisi (hepsi inline — 015); geçerse Id + MerchantKey üretir, `Status = Active` | `CreateMerchant.Handler` |
| `UpdateDetails` | `ResultDomain UpdateDetails(type, name, email, gsm, address, iban, contactName, contactSurname, identityNumber, taxOffice, taxNumber, legalCompanyTitle)` | `Create` ile aynı doğrulama seti (inline tekrar — bilinçli); `Id`/`MerchantKey`/`Status`/`SubMerchantKey` DEĞİŞMEZ | `UpdateMerchant.Handler` |
| `ChangeStatus` | `ResultDomain<bool> ChangeStatus(MerchantStatus newStatus)` | Serbest geçiş; aynı statü → `Ok(false)` (değişmedi, idempotent), farklı → `Ok(true)` (handler yalnız `true`'da event yayınlar — R5) | `ChangeMerchantStatus.Handler` |

İhlaller `MessageItem` (Code = resource sabiti) ile `Error` döner; exception yok.

## MerchantStatus (enum) — `Domains/Merchants/MerchantStatus.cs`

```
Active | Passive | Suspended
```

- Event'e `ToString()` ile string olarak taşınır ("Active"/"Passive"/"Suspended" —
  Identity handler'ı OrdinalIgnoreCase okur, BC enum'u Shared'a sızmaz).
- Token verme statü-kapılı: yalnız `Active` (mevcut Identity davranışı; bu faz değiştirmez).

### Statü geçişleri

```
        ┌──────────────────────────────┐
Create ──► Active ◄──► Passive         │
              ▲            ▲           │
              └──► Suspended ◄─────────┘   (üç statü arası serbest; aynı statüye geçiş no-op)
```

## MerchantType (enum) — `Domains/Merchants/MerchantType.cs`

```
Personal | PrivateCompany | LimitedOrJointStockCompany
```

Sağlayıcı `SubMerchantType` string sabitleri (`PERSONAL`, `PRIVATE_COMPANY`,
`LIMITED_OR_JOINT_STOCK_COMPANY`) ile eşleme İLERİKİ iyzico entegrasyon işinde yapılır;
bu fazda sağlayıcı tipi domain'e girmez.

## Integration event eşlemesi (mevcut sözleşme — DEĞİŞMEZ)

| Olay | Ne zaman | Taşınan |
|------|----------|---------|
| `MerchantCreated(MerchantId, MerchantKey, Status)` | `CreateMerchant` başarılı commit | Id + MerchantKey (tek sır taşıma noktası) + "Active" |
| `MerchantStatusChanged(MerchantId, NewStatus)` | `ChangeMerchantStatus` gerçek değişiklikte | Id + yeni statü string'i |

Yayın: `[Transactional]` handler içinden `IMessageBus.PublishAsync` → Wolverine outbox →
`merchant.lifecycle` fanout (Program.cs kayıtları mevcut). Tüketici:
`Identity.Server.MerchantClientEventHandler` (idempotent upsert — değişmez).
`MerchantProvisioned` bu fazda yayınlanmaz.
