# Contract: Store Onboarding REST (store + anonim)

Minimal API, kök sürüm `v1`. Store uçları `store-api-key` (X-Api-Key); form uçları anonim + per-session
`OpenToken` (fail-closed). Hiçbir dönüş credential/sır içermez (İlke VI — yalnız opak handle + durum).

## POST /store-onboarding  `[store-api-key]`

EC (super-admin adına) store aktivasyonunu başlatır.

**İstek** `{ merchantId: guid, correlationId: string, storeName: string }`

**Yanıt 200** `{ pgStoreRef: string (opak handle), formUrl: string, expiresAt: iso8601 }`
- `pgStoreRef` = opak tutamaç (credential/iç kimlik değil). `formUrl` = `FormBaseUrl` + `OpenToken`.

**Hata** `DUPLICATE_ONBOARDING` (aynı merchant için aktif süreç/Live var), `INVALID_REQUEST`.

## GET /store-onboarding/{openToken}  `[anonymous, fail-closed]`

Tek sabit hosted onboarding formunu render eder (HTML). Bilinmeyen/expired/kullanılmış token → 404
(sızıntısız). Form alanları = `StoreProfile` çekirdeği.

## POST /store-onboarding/{openToken}/submit  `[anonymous]`

Form gönderimi → profil bağlanır, bankalara fan-out.

**İstek** `{ legalName, taxNumber, iban, mcc, websiteUrl, contactName, contactEmail }`

**Yanıt 200** `{ pgStoreRef, status: "Submitted", banks: [{ bankCode, status: "Referred"|"Rejected", reasonCode? }] }`

**Hata** `INVALID_PROFILE` (alan/IBAN/MCC), `LINK_EXPIRED`, `ALREADY_SUBMITTED`.
- Banka-özel eksik alan → o banka `Rejected` (`BANK_FIELD_MISSING`), diğerleri `Referred`; submit yine 200.

## GET /store-onboarding/{pgStoreRef}/status  `[store-api-key]`

**Yanıt 200** `{ pgStoreRef, status: "Draft"|"Submitted"|"Live", banks: [{ bankCode, status, reasonCode? }] }`
- Credential/sır/iç token YOK. Red sebepleri görünür (US3).
