# Quickstart: Onboarding Aggregate Sadeleştirme Doğrulaması

Amaç: 5→2 konsolidasyondan SONRA onboarding davranışının aynen çalıştığını + yapısal ölçütlerin
tutunduğunu kanıtlamak. Bu bir davranış-korumalı refactor; senaryolar 013 quickstart'ının davranış
alt kümesidir + yeni `AwaitingDomainControl` görünürlüğü.

## Ön koşullar

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj   # Postgres + RabbitMQ + Identity + Mail.Mcp/Mailpit + BC'ler
```

- Dev DB sıfırlanabilir (migration yok). İlk kalkışta Marten şemayı yeni alanlarla üretir.
- Aday site (gerçek ECommerce) descriptor + challenge yayınını sunar.

## Yapısal doğrulama (statik — kod)

**S0a — Aggregate sayısı 5→2**:
```bash
grep -rlE "class .*: AggregateRoot" src/services/Merchant.Api/Domains
# BEKLENEN: yalnız RegisterRequest.cs, Merchant.cs, MerchantSettlementAccount.cs
# YOK: DomainControlChallenge.cs, ActivationTicket.cs, OnboardingNotification.cs
```

**S0b — Silinen klasörler yok**:
```bash
ls src/services/Merchant.Api/Domains/DomainControlChallenges 2>/dev/null   # yok
ls src/services/Merchant.Api/Domains/ActivationTickets 2>/dev/null         # yok
ls src/services/Merchant.Api/Domains/OnboardingNotifications 2>/dev/null    # yok
```

**S0c — Derleme + test**:
```bash
dotnet build                        # 0 hata (ölü referans yok)
dotnet test tests/Merchant.Api.Tests   # yeşil (challenge + activation testleri yeni sahiplerinde)
```

## Davranış senaryoları (canlı)

**S1 — Başvuru → AwaitingDomainControl (challenge henüz yok)**
- `submit_registration(descriptorUrl)` çağır; aday beklenen değeri HENÜZ yayınlamamış.
- BEKLENEN: `Status="ChallengeRequired"`, `Token`+`ExpectedValue`+`PublishPath` dolu, **`RequestId` dolu**.
- DB: tek `RegisterRequest`, `Status=AwaitingDomainControl`. Ayrı `DomainControlChallenge` dokümanı YOK.

**S2 — "Sürecim ne oldu?" (on-demand metin)**
- `registration_status(domain)` çağır.
- BEKLENEN: `Status="AwaitingDomainControl"`, `RequestId` dolu, **`Message`** metni sıradaki adımı bildirir.

**S3 — Değeri yayınla → tekrar başvur → Pending**
- Aday `ExpectedValue`'yu `/.well-known/merchant-challenge/{token}`'da yayınlar; `submit_registration` tekrar.
- BEKLENEN: `Status="Pending"`, `RequestId` AYNI talep (yeni talep açılmadı). DB: aynı RegisterRequest `Pending`.

**S4 — Mükerrer koruma**
- Aynı domain için `AwaitingDomainControl` veya `Pending` talep varken tekrar `submit_registration`.
- BEKLENEN: yeni talep açılmaz (aynı talep yeniden kullanılır / mükerrer RET).

**S5 — Admin onayı → merchant Provisioning + aktivasyon bileti (Merchant üstünde)**
- `POST /register-requests/{id}/approve`.
- BEKLENEN: `RegisterRequest.Status=Approved`; `Merchant` `Provisioning` doğar; `Merchant.ActivationToken`
  + `ActivationExpiresAtUtc` dolu; ayrı `ActivationTicket` dokümanı YOK. Aktivasyon maili Mailpit'te görünür.

**S6 — Redeem (tek-kullanım + TTL)**
- `POST merchants/activation/redeem { activationToken }`.
- BEKLENEN: `{ merchantId, merchantKey }` bir kez döner; `MerchantProvisioned` yayınlanır (Identity client kurar).
- İkinci redeem → RET (key yeniden dönmez). Süresi geçmiş token → RET.

**S7 — Aktivasyon → Active (3 koşul, değişmez)**
- Settlement hesabı ekle + komisyon grid hazır event'i + `ReturnUrl` set.
- BEKLENEN: `Merchant.TryActivate()` Provisioning→Active; `MerchantStatusChanged(Active)` yayınlanır.

**S8 — Ret yolu**
- `Pending` talebi `POST /register-requests/{id}/reject`.
- BEKLENEN: `Status=Rejected`; merchant doğmaz; o domainden yeni başvuru yapılabilir.

**S9 — Mail best-effort (OnboardingNotification yok)**
- S1/S5 mail tetiklerinde: mail Mailpit'e düşer, sonuç loglanır; DB'de `OnboardingNotification` dokümanı OLUŞMAZ.

## Kabul kanıtı

- S0a–S0c yapısal ölçütler tutar (SC-001/SC-004).
- S1–S9 davranış olarak 013 ile aynı (SC-003); tek görünür fark eklemeli (`RequestId` challenge
  aşamasında + `AwaitingDomainControl` + `Message`).
- Süreç tek yerden okunur: `RegisterRequest.Status` (SC-002).