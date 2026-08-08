# ECommerce Tarafı (E1) — Karşı Uç İşleri

> **Durum**: Bu dosya gateway (013) geliştirilirken tutulan **karşı-uç yapılacaklar** notu.
> Kapsam DIŞI (013 spec'inde E1 ayrı bağımlılık); canlı doğrulamada simüle edilir. Buradaki
> maddeler ECommerce repo'sunda (aday merchant sitesi) yapılacak işlerdir.
>
> Son güncelleme: 2026-08-08

## 1. Merchant Descriptor yayını — `/.well-known/merchant-descriptor.json`

Public keşif belgesi. Sır YOK, makine-okunur, stabil URL, additive versiyonlama.

```json
{
  "schemaVersion": "1.0",
  "domain": "shop.example.com",
  "legalName": "Örnek Ticaret A.Ş.",
  "taxId": "1234567890",
  "contactEmail": "onboarding@example.com",
  "webhookUrl": "https://shop.example.com/gateway/webhook",
  "agent": { "a2aCardUrl": "https://shop.example.com/.well-known/agent-card.json" }
}
```

- [X] Endpoint/dosya `/.well-known/merchant-descriptor.json` sun (HTTP GET, public). **UYGULANDI**:
      `src/ui/WebApp/GatewayOnboarding/GatewayOnboardingEndpoints.cs` (AllowAnonymous).
- [X] İçerik tek-kaynaktan (`appsettings` `GatewayOnboarding` section) — elle ikinci kopya yok.
- [X] Zorunlu alanlar dolu: `legalName`, `taxId`, `contactEmail`, `webhookUrl`.
- [X] Sır sızdırma yok: MerchantKey/token/secret descriptor'da değil.
- [ ] Additive versiyonla: alan ekle, silme/anlam değiştirme yok; `schemaVersion` minor artışı = yeni opsiyonel alan.

> **Gateway sözleşme notu (013 uygulama)**: `submit_registration` girdisi **descriptor'ın tam linki**
> (domain değil); challenge URL aynı origin'den türetilir. Aday site descriptorUrl'i gateway'e verir.

## 2. Domain-control challenge yayını — `/.well-known/merchant-challenge/{token}`

Descriptor'dan AYRI. HTTP-01 tarzı, başvuru anında dinamik.

- [X] Gateway başvuruda tek-kullanımlık `token` + `expectedValue` verir (ChallengeRequired yanıtı, ~1 saat TTL).
- [X] Aday site `/.well-known/merchant-challenge/{token}` yolunda `expectedValue`'yu düz metin yayınlar.
      **UYGULANDI**: challenge GET + `InMemoryChallengeStore`. Dev'de değeri yerleştirmek için
      `POST /gateway-onboarding/challenge {token, value}` (gateway'in döndürdüğü değeri koy).
- [X] Gateway senkron GET ile doğrular; geçerse RegisterRequest(Pending) oluşur.
- [X] İki-adım: 1) `submit_registration(descriptorUrl)` → ChallengeRequired; 2) değeri yayınla;
      3) `submit_registration` tekrar → doğrulanır. Süre dolarsa yeni token.
- [X] Format: `expectedValue` = gateway üretimi rastgele değer (fingerprint YOK — 013 sadeleşmesi).

## 3. Başvuru sürüşü — gateway'e otomatik kayıt

- [X] **UYGULANDI (A2A yerine MCP)**: `GatewayRegistrationClient` DropShop Merchant.Api `/mcp
      submit_registration`'ı **yapısal** çağırır (A2A/LLM metin ayrıştırmasından robust). İki-adımı
      otomatikler (ChallengeRequired → challenge'ı yerel store'a yaz → tekrar çağır → Pending).
      Tetik: `POST /gateway-onboarding/register`.
- [X] Auth: DropShop Identity client_credentials (`ecommerce-onboarding`, merchant.write; DropShop
      Config.cs'te seed). Config: `DropShopGateway:{IdentityAddress,McpUrl,ClientId,ClientSecret}`.
      **Dev not**: `McpUrl` DropShop Merchant.Api'nin dinamik portu — DropShop Aspire dashboard'dan doldur.
- [ ] (Opsiyonel) Gerçek A2A istemcisi (Merchant.Agent card keşfi) — 013'te MCP yolu yeterli; ertelendi.
- [X] **013'te komisyon müzakeresi YOK** (gateway-otoriter) — E1'de komisyon mesajı gerekmez.

## 4. MerchantKey saklama (GÜVENLİK)

MerchantKey = gateway'de OAuth `client_secret` (`client_id=merchantId`, client_credentials).
Kimlik sırrı gibi davran:

- [X] **UYGULANDI (dev)**: `IMerchantCredentialStore` (bellek-içi) + `POST /gateway-onboarding/merchant-key
      {merchantId, merchantKey}`. Aktivasyon sayfasında bir kez gösterilen key insan tarafından girilir.
- [X] Yalnız sunucu tarafı; tarayıcı/JS'e gitmez.
- [X] Aktivasyon sayfasında bir kez gösterilir → insan kopyalar → store'a koyar.
- [ ] **PROD sertleştirme (kaldı)**: bellek-içi yerine secret store (user-secrets/env/vault); DB'ye
      konacaksa şifreli; log/telemetri sızdırma yok; rotasyon G5; 15 dk token cache (−30sn yenileme).
      Charge (G5) bu key'le `connect/token`'a gider.

## 5. ReturnUrl + externalRef (ileri, charge G5)

- [ ] ReturnUrl: geçerli HTTPS ödeme dönüş adresi — gateway'e bildirilecek (Active koşulu #3).
- [ ] externalRef: opak referans (sipariş/müşteri karşılığı) — isteklerde iletilir, gateway
      anlamlandırmaz, aynen geri döner. Eşlemeyi ECommerce kendi tarafında yapar.

## Açık uçlar / gateway tarafı netleşince yansıt

- Challenge dosyasının kesin içerik/format kontratı (gateway `contracts/` üretince).
- A2A başvuru mesaj şeması (Merchant.Agent card skills netleşince).
- Webhook payload sözleşmesi (charge G5 ile genişleyecek — şimdilik sadece adres beyanı).