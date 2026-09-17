# Feature Specification: Admin OAuth Düzlemi (G3 — insan/rol düzlemi)

**Feature Branch**: `042-admin-oauth-plane`

**Created**: 2026-09-17

**Status**: Draft

**Input**: User description: "ECommerce'teki gibi tüm endpoint'leri MCP'ye dönüştürmek istiyorum
(istisna olabilir), Agents klasörü altında işlemleri yapmak istiyorum. Admin OAuth altyapısı hiç
yok — ECommerce'in 061+070 desenini birebir kur."

## Bağlam

Bu, daha büyük bir programın (Merchant/Commission admin REST yüzeyini MCP'ye taşıma, Admin Razor
UI + Merchant.Agent'ı sökme) **ilk ve ön koşul** dilimidir. Bu dilim yalnız Identity.Server'a insan
login + admin OAuth primitiflerini kurar; Merchant.Api/Commission.Api'nin `/mcp-admin` uçları ve
Admin UI/Merchant.Agent söküm işi **ayrı, sonraki spec'lerdir** (042'ye bağımlı).

Anayasa `TODO(AUTHZ_MODEL)` G3 (insan/rol düzlemi) bu dilimle kapanır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin, Claude Desktop ile PG'ye login olur (Priority: P1)

PG sahibi (tek admin), Claude Desktop'ı PG'nin Merchant.Api `/mcp-admin` ucuna bağlamak ister.
Claude Desktop OAuth authorization_code + PKCE akışını başlatır; tarayıcı Identity.Server'ın login
sayfasına yönlenir; admin e-posta+parola girer; Claude Desktop'a access token döner (consent ekranı
YOK — ECommerce'te de seed istemciler `ConsentType=Implicit`, yalnız DCR/dinamik istemciler
Explicit consent görür; bizim istemcimiz seed'li, ilk-taraf).

**Why this priority**: Bu olmadan hiçbir admin MCP tool'u insan tarafından çağrılamaz — tüm sonraki
dilimlerin (Merchant/Commission `/mcp-admin`, Admin UI söküm) önkoşulu.

**Independent Test**: Identity.Server + boş bir test MCP ucu (mevcut Merchant.Api `/mcp`'yi geçici
hedef alarak) ile uçtan uca OAuth akışı canlı denenebilir; token alınması + `merchant_id` claim'i
TAŞIMAMASı doğrulanır (admin token, merchant token değil).

**Acceptance Scenarios**:

1. **Given** admin hiç login olmamış, **When** Claude Desktop'tan `/mcp-admin`'e bağlanmayı dener,
   **Then** 401 + RFC 9728 `WWW-Authenticate: Bearer resource_metadata=...` döner, tarayıcı login
   sayfasına yönlenir.
2. **Given** admin doğru email+parola girer, **When** login tamamlanır, **Then** consent ekranı
   ATLANIR (seed istemci, `ConsentType=Implicit`) ve Claude Desktop authorization code'u token'a
   çevirir; dönen access token `merchant_id` claim'i TAŞIMAZ.
3. **Given** admin yanlış parola girer, **When** login denenir, **Then** hata gösterilir, token
   verilmez.

---

### User Story 2 - Bootstrap admin açılışta idempotent seed edilir (Priority: P1)

Sistem ilk kez ayağa kalktığında (veya her restart'ta), config'te tanımlı bootstrap admin (email +
parola placeholder) `ApplicationUser` olarak yoksa oluşturulur; varsa dokunulmaz (admin'in sonradan
değiştirdiği parola ezilmez).

**Why this priority**: Login akışının çalışması için en az bir kullanıcı hesabı şart; elle
kayıt/registration akışı bu dilimin kapsamında DEĞİL (tek admin, self-servis kayıt yok).

**Independent Test**: `BootstrapAdmin:Email`/`BootstrapAdmin:Password` config'i boşken açılış
sorunsuz tamamlanır (kullanıcı oluşturulmaz); doluyken bir kullanıcı oluşur; ikinci açılışta
mükerrer oluşturma denemesi yapılmaz.

**Acceptance Scenarios**:

1. **Given** `BootstrapAdmin` config'i boş, **When** Identity.Server açılır, **Then** hiçbir
   kullanıcı oluşturulmaz, açılış hata vermez.
2. **Given** config dolu ve kullanıcı yok, **When** açılır, **Then** kullanıcı oluşur.
3. **Given** kullanıcı zaten var (parolası admin tarafından değiştirilmiş), **When** yeniden açılır,
   **Then** parola ESKİ HALİYLE kalır (seed ezmez).

---

### User Story 3 - Loopback + Claude callback redirect URI kabul edilir (Priority: P2)

MCP Inspector/mcp-remote gibi araçlar dinamik portlu `http://localhost:<port>` callback kullanır;
Claude Desktop ise sabit `https://claude.ai/api/mcp/auth_callback` / `https://claude.com/...`
kullanır. Seed edilen `external-admin-agent` istemcisi ikisini de kabul eder.

**Why this priority**: Gerçek istemci (Claude Desktop + geliştirme sırasında mcp-remote/Inspector)
farklı redirect URI'ler kullanır; ikisi de çalışmazsa akış test edilemez/kullanılamaz.

**Independent Test**: `external-admin-agent` client'ıyla hem `http://localhost:<rastgele-port>/...`
hem `https://claude.ai/api/mcp/auth_callback` redirect_uri'siyle authorize isteği denenir, ikisi de
kabul edilir; başka bir domain/host reddedilir.

**Acceptance Scenarios**:

1. **Given** redirect_uri `http://localhost:54231/callback`, **When** authorize istenir, **Then**
   kabul edilir (yalnız `external-admin-agent` client'ı için).
2. **Given** redirect_uri `https://evil.example.com/callback`, **When** authorize istenir, **Then**
   reddedilir.

### Edge Cases

- Admin parolasını unutursa ne olur? → Kapsam dışı (v1: config'ten `BootstrapAdmin:Password`'ü
  değiştirip restart — self-servis şifre sıfırlama yok, tek-kullanıcılı sistem).
- Consent'i reddederse? → Standart OpenIddict akışı: `access_denied` hatası, token verilmez.
- Birden fazla admin gerekirse? → Kapsam dışı (v1 tek bootstrap admin; çoklu-kullanıcı/RBAC ayrı
  spec, ECommerce'in 030'u örnek alınabilir ama şimdi YAGNI).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Identity.Server, mevcut `client_credentials` akışına EK olarak `authorization_code`
  akışını (PKCE zorunlu) desteklemek ZORUNDADIR.
- **FR-002**: Sistem, config'te tanımlı bootstrap admin (email+parola) yoksa açılışta idempotent
  şekilde oluşturmak ZORUNDADIR; config boşsa kullanıcı oluşturmamalıdır; mevcut kullanıcının
  parolasını yeniden başlatmada EZMEMELİDİR.
- **FR-003**: Sistem minimal bir login sayfası sunmak ZORUNDADIR (email+parola, ASP.NET Identity
  cookie tabanlı); rol/kayıt/self-servis şifre sıfırlama YOKTUR (v1 kapsam dışı).
- **FR-004**: Sistem consent ekranı SUNMAMALIDIR (`external-admin-agent` seed istemcisi
  `ConsentType=Implicit` — ECommerce'te de yalnız DCR/dinamik istemciler Explicit consent görür,
  seed istemciler görmez; birebir aynı davranış).
- **FR-005**: Sistem seed edilen `external-admin-agent` istemcisini (public, PKCE, secret'sız,
  `AllowAuthorizationCode`+`AllowRefreshToken`) idempotent oluşturmak/güncellemek ZORUNDADIR.
- **FR-006**: `external-admin-agent` istemcisinin redirect URI doğrulaması, birebir eşleşen Claude
  callback URI'lerini (`https://claude.ai/api/mcp/auth_callback`, `https://claude.com/api/mcp/auth_callback`)
  VE herhangi portlu `http://localhost|127.0.0.1` loopback URI'sini kabul etmek ZORUNDADIR; başka
  istemciler bu muafiyetten YARARLANAMAZ.
- **FR-007**: Verilen access token, `merchant_id` claim'i TAŞIMAMALIDIR (mevcut `AdminPlaneOnly`
  politikasının bu token'ları admin-düzlemi olarak kabul etmesi için — downstream değişiklik YOK).
- **FR-008**: RFC 9728 protected-resource metadata uzantısının (`Common`) taşınması bu dilimin
  KAPSAMI DIŞINDADIR (YAGNI — henüz tüketicisi yok); gerçek tüketicisi olan Merchant.Api
  `/mcp-admin` sub-projesinde (#2) o dilimin kendi işi olarak taşınır/yazılır.
- **FR-009**: `external-admin-agent` istemcisinin scope demeti `openid`, `profile`, `merchant.read`,
  `merchant.write`, `commission.read`, `commission.write` ile SINIRLI olmalıdır (kapalı demet;
  genişleme ayrı karar).

### Key Entities

- **ApplicationUser (mevcut, kullanılmaya başlanıyor)**: ASP.NET Identity kullanıcısı; bu dilimde
  yalnız TEK bootstrap admin kaydı yaşar. Rol/claim genişlemesi kapsam dışı.
- **external-admin-agent (OpenIddict istemcisi, yeni seed)**: Public+PKCE istemci; Claude
  Desktop'ın admin bağlantısını temsil eder.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, Claude Desktop'tan tek login ile (consent ekranı YOK) access token alır.
- **SC-002**: Alınan token `merchant_id` claim'i taşımaz; mevcut `AdminPlaneOnly` policy'si
  DEĞİŞMEDEN bu token'ı kabul eder (regresyon yok — kod değişikliği gerekmediği canlı doğrulanır).
- **SC-003**: `BootstrapAdmin` config'i boşken açılış hatasız tamamlanır (mevcut davranış korunur).
- **SC-004**: Loopback (`mcp-remote`/Inspector) VE Claude Desktop sabit callback'i ikisi de canlı
  authorize akışını tamamlar.

## Assumptions

- Tek admin yeterli (v1); çoklu-kullanıcı/RBAC/self-servis şifre sıfırlama kapsam dışı, YAGNI.
- `AdminPlaneOnly` politikası DEĞİŞMEZ (negatif kontrol zaten insan token'ını kabul eder) —
  doğrulandı, kod tabanından teyitli.
- Merchant.Api/Commission.Api'nin `/mcp-admin` uçlarını AÇMASI ve bu istemcinin scope'larıyla
  gerçek tool çağırması bu spec'in DIŞINDA (ayrı, bağımlı sonraki spec).
- Admin UI (`src/ui/Admin`) ve Merchant.Agent söküm işi bu spec'in DIŞINDA (ayrı, bağımlı sonraki
  spec — eski yüzey yeni yüzey canlı çalışana kadar sökülmez).
- Bootstrap admin email/parola placeholder bırakılır; kullanıcı sonradan `dotnet user-secrets` ile
  kendi değerini girer (ECommerce'in `BootstrapAdmin` deseniyle birebir).
