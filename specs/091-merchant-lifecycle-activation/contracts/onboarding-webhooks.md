# Contract: Onboarding Webhook'ları (banka→PG inbound + PG→store outbound)

İki yön. Her ikisi HMAC-SHA256 ham-gövde imzalı (`X-Signature` lowercase hex), ham body hem imzalanır
hem gönderilir, alıcı AYNI ham body'yi aynı sırayla doğrular. Sır (imza anahtarı, credential) ASLA
log/trace/dönüş DTO'sunda (İlke VI).

## Inbound — banka → PG (sonuç + credential)

`POST /internal/onboarding/callback/{callbackToken}`  `[anonymous, per-session token + HMAC]`

- **Kapı:** path'teki `CallbackToken` ilgili `StoreOnboarding`'in token'ı ile eşleşmezse reddet (durum değişmez). `X-Signature` = HMAC-SHA256(`InterVposBankProfile.InboundCallbackSecret`, rawBody) doğrulanır; geçersizse `CALLBACK_REJECTED`.
- **Gövde (banka-genel; her banka wire'ı adapter `NormalizeCallback`'inde çözülür):**
  ```
  { bankCode, outcome: "Approved"|"Rejected", reasonCode?, bankApplicationRef?,
    credential?: { shopCode, userCode, userPass, secretKey?, subscriptionKey? } }
  ```
- **İşlem:** adapter normalize → `OnboardingOutcome`; `Approved`+credential → `IBankCredentialStore.Save` → `CredentialRef`; `RecordBankOutcome`. İlk onayda `PaymentActivated` event + `OnboardingResultDelivery` (store'a). Idempotent: tekrar gelen Approved no-op.
- **Değişmez:** credential yalnız bu imzalı gövdede akar; yanıtta/logda ASLA. Yanıt `200` gövdesiz (ya da `{ "received": true }`).

## Outbound — PG → store (aktivasyon sonucu)

`OnboardingResultDelivery.Deliver` (Wolverine durable local queue; `StoreResultDelivery` aynası).

- **Gövde** `{ pgStoreRef, merchantId, status: "Live", activatedBankCode, activatedAt }`
- **İmza** `X-Signature` = HMAC-SHA256(`OnboardingStoreCallbackOptions.CallbackSecret`, rawBody); POST → non-2xx throw → durable retry. `CallbackSecret` secret-store.
- **Değişmez:** credential İÇERMEZ (yalnız "canlı oldun" + hangi banka). Store aynı ham body'yi doğrular.
