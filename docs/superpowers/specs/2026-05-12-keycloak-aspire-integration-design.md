# Keycloak + Aspire Entegrasyon Tasarımı

**Tarih:** 2026-05-12  
**Kapsam:** Aspire AppHost'a Keycloak eklenmesi, IAM.Api'nin UserManagement'a dönüştürülmesi, tüm servislerin Keycloak JWT doğrulamasına geçişi

---

## Motivasyon

IAM.Api şu an custom JWT üretimi ve şifre hash'leme yapıyor. Keycloak, kimlik doğrulama sorumluluğunu devralır; IAM servisi **UserProfile yönetimi** (rol atama, merchant ilişkisi, kullanıcı durumu) odaklı hale gelir.

---

## Mimari Genel Bakış

```
Client
  │
  ├─→ POST /realms/payment-gateway/protocol/openid-connect/token  →  Keycloak
  │                                                                     │
  │   access_token (JWT) ←──────────────────────────────────────────────┘
  │
  ├─→ API isteği (Bearer token)  →  PaymentProcessing / IAM / vb.
  │                                   │
  │                                   └─ JWT doğrulama (lokal, JWKS cache)
  │
  └─→ POST /api/users (kayıt)    →  IAM.Api
                                     │
                                     ├─→ Keycloak Admin API (kullanıcı oluştur)
                                     └─→ Marten (UserProfile kaydet)
```

Diğer servisler Keycloak ile doğrudan iletişim kurmaz; sadece token'daki imzayı JWKS endpoint'inden çektiği public key ile doğrular.

---

## 1. AppHost Değişiklikleri

### 1.1 Paket

`AppHost.csproj`'a eklenir:
```xml
<PackageReference Include="Aspire.Hosting.Keycloak" Version="9.3.0" />
```

### 1.2 AppHost.cs

```csharp
var keycloak = builder.AddKeycloak("keycloak", port: 8080)
    .WithRealmImport("./keycloak/realms")
    .WithLifetime(ContainerLifetime.Persistent);

// IAM servisi Keycloak Admin API'ye erişmesi gerektiğinden referans alır
var iamApi = builder.AddProject<Projects.IAM_Api>("iam")
    .WithReference(keycloak)
    .WithReference(iamDb).WithReference(redis).WithReference(rabbitmq)
    .WaitFor(keycloak).WaitFor(iamDb).WaitFor(redis).WaitFor(rabbitmq);

// Diğer servisler Keycloak'a referans almaz; Authority URL config'den gelir
```

### 1.3 Realm JSON

Konum: `AppHost/keycloak/realms/payment-gateway.json`

```json
{
  "realm": "payment-gateway",
  "enabled": true,
  "clients": [
    {
      "clientId": "payment-api",
      "enabled": true,
      "publicClient": false,
      "directAccessGrantsEnabled": true,
      "serviceAccountsEnabled": true,
      "secret": "payment-api-secret",
      "defaultClientScopes": ["openid", "email", "profile"]
    }
  ],
  "roles": {
    "realm": [
      { "name": "admin" },
      { "name": "merchant_user" }
    ]
  }
}
```

`directAccessGrantsEnabled: true` sadece dev ortamı içindir (Resource Owner Password flow). Prod'da kaldırılır.  
`serviceAccountsEnabled: true` IAM servisinin Admin API'ye client credentials ile erişmesi içindir.

---

## 2. ServiceDefaults — JWT Doğrulama

`ServiceDefaults` projesine extension eklenir:

```csharp
public static IHostApplicationBuilder AddKeycloakJwtAuthentication(
    this IHostApplicationBuilder builder)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Keycloak:Authority"];
            // Örnek: http://localhost:8080/realms/payment-gateway
            options.Audience = "payment-api";
            options.RequireHttpsMetadata = false; // dev only
        });

    builder.Services.AddAuthorization();
    return builder;
}
```

Her servisin `Program.cs`'ine:
```csharp
builder.AddKeycloakJwtAuthentication();
// pipeline:
app.UseAuthentication();
app.UseAuthorization();
```

Keycloak'ın JWKS endpoint'i (`/realms/payment-gateway/protocol/openid-connect/certs`) ilk token doğrulamada otomatik çekilir ve cache'lenir. Keycloak'a her istekte çağrı yapılmaz.

### ICurrentUser Güncellemesi

`AuthExtensions.cs` içindeki `CurrentUser.Load` metodu Keycloak claim yapısına uyarlanır:

```csharp
public static ICurrentUser Load(ClaimsPrincipal principal) => new CurrentUser
{
    Id    = Guid.Parse(principal.FindFirstValue("sub")!),
    Email = principal.FindFirstValue("email")!,
    Name  = principal.FindFirstValue("given_name")
          + " " + principal.FindFirstValue("family_name")
};
```

`JwtPermissionFilter` değişmez — `currentUser.Id == Guid.Empty` kontrolü aynen geçerlidir.

---

## 3. User Domain Modeli

Şifre sorumluluğu Keycloak'a geçtiğinden `User` aggregate'i sadece **UserProfile** verisi tutar.

### Kaldırılanlar

- `PasswordHash Password` field'ı
- `bool Login(string plainPassword)` metodu
- `ResultDomain ChangePassword(string newPlainPassword)` metodu
- `PasswordHash.cs` value object dosyası

### User.Create İmzası

```csharp
public static ResultDomain<User> Create(
    Guid keycloakId,   // Keycloak sub → User.Id olarak set edilir
    string email,
    string firstName,
    string lastName,
    Guid? merchantId = null)
```

### Sonuç Yapısı

```
User (AggregateRoot)
  Id          → Keycloak sub (dışarıdan verilir)
  Email
  FullName
  Status      → Active / Passive
  MerchantId
  Roles []    → UserRole list

  Metotlar:
    Activate / Deactivate
    AssignRole / RemoveRole
    AssignMerchant / RemoveFromMerchant
```

---

## 4. IAM.Api Komut Değişiklikleri

### 4.1 Yeni: KeycloakAdminClient

`IAM.Api/Keycloak/KeycloakAdminClient.cs`

```csharp
public class KeycloakAdminClient(HttpClient http, IKeycloakTokenProvider tokenProvider)
    : ITransientDependency
{
    public Task<Guid> CreateUserAsync(
        string email, string password, string firstName, string lastName);

    public Task ResetPasswordAsync(Guid keycloakId, string newPassword);

    public Task DeleteUserAsync(Guid keycloakId);
}
```

### 4.2 Yeni: KeycloakTokenProvider

`IAM.Api/Keycloak/KeycloakTokenProvider.cs`

Client credentials flow ile Admin API token'ı alır. `ITransientDependency` implement eder, `DependencyExtensions` scan'i otomatik yakalar.

### 4.3 CreateUser — Güncellenir

```
1. KeycloakAdminClient.CreateUserAsync(email, password, firstName, lastName)
     → keycloakId (Guid) döner
2. User.Create(keycloakId, email, firstName, lastName)
     → Marten'a kaydedilir
3. Keycloak başarılı ama Marten başarısız olursa:
     → KeycloakAdminClient.DeleteUserAsync(keycloakId)  (compensating call)
```

### 4.4 ChangePassword — Güncellenir

```
1. session.LoadAsync<User>(cmd.UserId)   → kullanıcı var mı kontrolü
2. KeycloakAdminClient.ResetPasswordAsync(cmd.UserId, cmd.NewPassword)
   (Marten'a yazılacak bir şey yok)
```

### 4.5 Login — Silinir

Login endpoint ve `Login` komutu kaldırılır. Client doğrudan Keycloak token endpoint'ini çağırır:

```
POST /realms/payment-gateway/protocol/openid-connect/token
  Content-Type: application/x-www-form-urlencoded

  grant_type=password
  &username=user@example.com
  &password=secret
  &client_id=payment-api
  &client_secret=payment-api-secret
```

---

## 5. Silinen Dosyalar

| Dosya | Neden |
|---|---|
| `IAM.Api/Domains/Users/Features/Commands/Login.cs` | Keycloak halleder |
| `IAM.Api/Domains/Users/Features/Endpoints/AuthEndpoints.cs` | Login endpoint kalkar |
| `IAM.Api/Domains/Users/ValueObjects/PasswordHash.cs` | Domain'de şifre yok |

---

## 6. Config

`IAM.Api/appsettings.json`:
```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/payment-gateway",
    "AdminApiBaseUrl": "http://localhost:8080",
    "Realm": "payment-gateway",
    "ClientId": "payment-api",
    "ClientSecret": "payment-api-secret"
  }
}
```

Aspire otomatik injection ile `Authority` değeri servis discovery'den de gelebilir.

---

## 7. Kapsam Dışı (Sonraki Adımlar)

- Token enrichment (merchantId, roles claim'e eklenmesi) — Keycloak custom mapper
- Prod'da `RequireHttpsMetadata = true` ve secret yönetimi
- Refresh token akışı
- Kullanıcı devre dışı bırakıldığında Keycloak'ta da disable etme (`ActivateUser` / `DeactivateUser`)