# YARP Gateway Design

**Date:** 2026-05-15  
**Branch:** feat/microservices-migration  
**Status:** Approved

---

## Motivation

İstemci tarafında her mikroservis için ayrı URL tanımlamak yerine tek bir giriş noktası sağlamak. Kimlik doğrulama (Keycloak JWT ve API Key) ve cache mekanizmasını merkezileştirmek. Rate limiting için altyapıyı hazır hale getirmek (scope dışı, ilerleyen aşamada).

---

## Mimari

Yeni `Gateway.Api` projesi oluşturulur ve Aspire AppHost'a eklenir. Tüm dış trafik gateway üzerinden geçer; downstream servisler dışa kapalı kalır.

```
İstemci
   │
   ▼
Gateway.Api (YARP)
   ├── /auth/**          → IAM.Api
   ├── /merchants/**     → MerchantManagement.Api
   ├── /banks/**         → BankIntegration.Api
   ├── /commissions/**   → CommissionManagement.Api
   ├── /settlements/**   → Settlement.Api
   └── /payments/**      → PaymentProcessing.Api
```

**Middleware pipeline sırası:**

```
Request → RouteMatching → AuthMiddleware (Keycloak veya ApiKey) → HeaderInjection → YARP Proxy → Downstream
```

**Aspire:** Gateway tüm servislere `WithReference` alır. Downstream URL'ler Aspire service discovery ile çözülür, hardcode olmaz.

---

## Middleware Pipeline

Route'lar iki gruba ayrılır. Hangi grubun çalışacağı YARP route metadata'sındaki `AuthType` alanıyla belirlenir.

### Grup 1 — Keycloak JWT Routes

Kapsamı: `/auth/**`, `/merchants/**`, `/banks/**`, `/commissions/**`, `/settlements/**`

```
KeycloakAuthMiddleware
  → JWT doğrular (Keycloak public key ile)
  → Geçersizse 401
  → Geçerliyse header ekler:
      X-User-Id     : sub claim
      X-User-Email  : email claim
      X-User-Roles  : roles claim (comma-separated)
  → YARP downstream'e forward eder
```

### Grup 2 — API Key Routes

Kapsamı: `/payments/**`

```
ApiKeyMiddleware
  → Header'dan X-Api-Key okur
  → Redis'te lookup yapar (ApiKey → MerchantInfo)
    - Miss ise MerchantManagement.Api'ye gRPC çağrısı → Redis'e yazar
    - Merchant Suspended/Passive ise 403
    - Key Revoked/Expired ise 401
  → Geçerliyse header ekler:
      X-Merchant-Id   : merchant id
      X-Merchant-Name : merchant name
  → YARP downstream'e forward eder
```

**Login (token alma):** İstemci doğrudan Keycloak'a bağlanır (`http://keycloak:8080/realms/payment-gateway/protocol/openid-connect/token`). Gateway bu akışı proxy'lemez; Keycloak gateway'in arkasında değildir.

---

## Cache & Invalidation

### Redis Cache Yapısı

```
Key   : apikey:{apiKeyValue}
Value : { MerchantId, MerchantName, MerchantStatus, ApiKeyStatus }
TTL   : Yok (event-driven invalidation)

Key   : merchant:{merchantId}:keys
Value : Set<apiKeyValue>   (reverse lookup için)
```

`apikey:{value}` için `Common/Caching` (`ICache`) kullanılır. `merchant:{merchantId}:keys` reverse lookup için Redis Set gereklidir; `ICache` soyutlaması Set operasyonlarını desteklemediğinden bu alan için `IConnectionMultiplexer` (StackExchange.Redis) doğrudan kullanılır.

### Cache Doldurma (Cold Start)

```
ApiKeyMiddleware
  → Redis'te key yok (cache miss)
  → MerchantManagement.Api'ye gRPC çağrısı
  → Sonucu Redis'e yazar (apikey:{value} + merchant:{id}:keys set'ine ekler)
  → İstek devam eder
```

### Cache Invalidation (Event-driven)

`SharedContracts`'a iki yeni integration event eklenir:

| Event | Publisher | Consumer | Tetikleyici |
|---|---|---|---|
| `ApiKeyRevoked` | MerchantManagement.Api | Gateway.Api | `RevokeApiKey` command |
| `MerchantStatusChanged` | MerchantManagement.Api | Gateway.Api | `SuspendMerchant`, `DeactivateMerchant`, `ActivateMerchant` |

**Gateway Wolverine consumer'ları:**

```
ApiKeyRevokedHandler
  → Redis'ten apikey:{value} siler
  → merchant:{id}:keys set'inden value'yu çıkarır

MerchantStatusChangedHandler
  → merchant:{id}:keys set'inden tüm key'leri okur
  → Her apikey:{value} entry'sini MerchantStatus günceller
    (Suspended/Passive ise sonraki isteklerde 403 döner)
```

---

## Proje Yapısı

```
Gateway.Api/
├── Program.cs
├── Dependencies/
│   └── DependencyExtensions.cs
├── Middleware/
│   ├── KeycloakAuthMiddleware.cs
│   └── ApiKeyMiddleware.cs
├── EventHandlers/
│   ├── ApiKeyRevokedHandler.cs
│   └── MerchantStatusChangedHandler.cs
├── Models/
│   └── MerchantCacheEntry.cs
└── appsettings.json
```

---

## AppHost Değişimi

```csharp
var gateway = builder.AddProject<Projects.Gateway_Api>("gateway")
    .WithReference(redis)
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WithReference(iamApi)
    .WithReference(merchantApi)
    .WithReference(bankIntApi)
    .WithReference(commissionApi)
    .WithReference(settlementApi)
    .WithReference(paymentApi)
    .WithEnvironment("Keycloak__Authority", "http://localhost:8080/realms/payment-gateway")
    .WaitFor(redis).WaitFor(rabbitmq).WaitFor(keycloak)
    .WaitFor(iamApi).WaitFor(merchantApi).WaitFor(bankIntApi)
    .WaitFor(commissionApi).WaitFor(settlementApi).WaitFor(paymentApi);
```

Downstream servisler dışarıya port expose etmez — sadece gateway erişir.

---

## YARP Konfigürasyonu (appsettings.json)

```json
"ReverseProxy": {
  "Routes": {
    "payments-route": {
      "ClusterId": "payment-processing",
      "Match": { "Path": "/payments/{**catch-all}" },
      "Metadata": { "AuthType": "ApiKey" }
    },
    "auth-route": {
      "ClusterId": "iam",
      "Match": { "Path": "/auth/{**catch-all}" },
      "Metadata": { "AuthType": "Keycloak" }
    },
    "merchants-route": {
      "ClusterId": "merchant-management",
      "Match": { "Path": "/merchants/{**catch-all}" },
      "Metadata": { "AuthType": "Keycloak" }
    },
    "banks-route": {
      "ClusterId": "bank-integration",
      "Match": { "Path": "/banks/{**catch-all}" },
      "Metadata": { "AuthType": "Keycloak" }
    },
    "commissions-route": {
      "ClusterId": "commission-management",
      "Match": { "Path": "/commissions/{**catch-all}" },
      "Metadata": { "AuthType": "Keycloak" }
    },
    "settlements-route": {
      "ClusterId": "settlement",
      "Match": { "Path": "/settlements/{**catch-all}" },
      "Metadata": { "AuthType": "Keycloak" }
    }
  },
  "Clusters": {
    "payment-processing": {
      "Destinations": { "primary": { "Address": "http://payment-processing" } }
    },
    "iam": {
      "Destinations": { "primary": { "Address": "http://iam" } }
    },
    "merchant-management": {
      "Destinations": { "primary": { "Address": "http://merchant-management" } }
    },
    "bank-integration": {
      "Destinations": { "primary": { "Address": "http://bank-integration" } }
    },
    "commission-management": {
      "Destinations": { "primary": { "Address": "http://commission-management" } }
    },
    "settlement": {
      "Destinations": { "primary": { "Address": "http://settlement" } }
    }
  }
}
```

---

## Downstream Servis Değişiklikleri

| | Kaldırılan | Eklenen |
|---|---|---|
| Her servis (Keycloak) | `AddKeycloakJwtAuthentication()` | `AddGatewayIdentity()` (ServiceDefaults'a eklenir) |
| PaymentProcessing | JWT claim okuma (`MerchantMiddleware`) | Header okuma (`X-Merchant-Id`, `X-Merchant-Name`) |
| MerchantManagement | — | `ApiKeyRevoked` ve `MerchantStatusChanged` event publish |
| SharedContracts | — | `ApiKeyRevoked`, `MerchantStatusChanged` event record'ları |
| AppHost | — | `Gateway.Api` projesi |

`ICurrentUser.Load` JWT claim yerine `X-User-Id`, `X-User-Email`, `X-User-Roles` header'larını okur.

---

## SharedContracts Yeni Events

```csharp
public record ApiKeyRevoked(string ApiKeyValue, Guid MerchantId);

public record MerchantStatusChanged(
    Guid MerchantId,
    MerchantStatus NewStatus,
    IReadOnlyList<string> ApiKeyValues);
```

---

## Scope Dışı (İlerleyen Aşama)

- **Rate Limiting** — Per API key ve/veya per route. Gateway middleware pipeline'a eklenmesi kolaydır.
- **Request/Response Logging** — Merkezi audit log.
- **Circuit Breaker** — Downstream servis hatalarında gateway seviyesinde Polly entegrasyonu.