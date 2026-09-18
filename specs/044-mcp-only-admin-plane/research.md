# Research: MCP-Only Admin Düzlemi (044)

Kod taraması 2026-09-18; tüm kararlar mevcut kod + 043 desenleri üzerinden verildi, dış
araştırma gerektirmedi.

## R1 — MerchantScoped uçlar sökümden muaf

**Decision**: `GetMerchant` (GET /merchants/{id}) ve `GetCommissionPolicy`
(GET /commission-policies/{merchantId}) KALIR.

**Rationale**: İkisi de `MerchantScoped` policy'li — merchant kendi token'ıyla kendi kaydını/
politikasını okur (EC tarafı tüketici; GetMerchant MerchantKey bile döner, 023 bilinçli dev
kararı). Admin yüzeyi değil, merchant self-service düzlemi. Spec FR-011 buna göre eklendi.

**Alternatives considered**: Tümünü sökmek (spec ilk hali "5 uç") — MerchantScoped tüketiciyi
kırardı, reddedildi.

## R2 — CalculateEffectiveCommission: tamamen silinir

**Decision**: `CalculateEffectiveCommission` slice'ı (REST ucu + sorgu) TAMAMEN silinir; MCP
tool'u AÇILMAZ (kullanıcı kararı: kullanılmayan işlev gider).

**Rationale**: Uç AdminPlaneOnly ama Admin UI istemcisi hiç çağırmıyor (ölü kod). Hesaplama
mantığı aggregate'te (`CommissionPolicy`) yaşamaya devam eder; ihtiyaç doğarsa ileride agent
slice'ı olarak yeniden açılır (JIT ilkesi — endpoint gerçek tüketici çıkınca açılır).

**Alternatives considered**: `admin_calculate_commission` MCP tool'u — öneri sunuldu,
kullanıcı reddetti (canlı ihtiyaç yok, YAGNI).

## R3 — Hassas/hassas-dışı güncelleme ile UpdateDetails ilişkisi

**Decision**: Aggregate'in mevcut `UpdateDetails` metodu DOKUNULMADAN kullanılır. İki handler da
(MCP `AdminUpdateMerchant` = hassas-dışı; BFF `UpdateMerchantSensitive` = hassas) önce
aggregate'i yükler, kendi kapsamı DIŞINDAKİ alanları mevcut değerlerden geçirir.

**Rationale**: `UpdateDetails` tüm alanları tek imzada alır ve invariant'ları (email/IBAN regex,
zorunluluk, tip-koşullu TCKN/vergi kuralları) tek yerde tutar. Alan-bazlı iki ayrı aggregate
metodu invariant'ı bölerdi; "yeni kural → aggregate" ilkesi mevcut metotla zaten sağlanıyor.

**Alternatives considered**: `UpdateNonSensitive`/`UpdateSensitive` ayrı metotlar — tip-koşullu
kurallar (Individual→TCKN, Company→vergi) iki metoda yayılıp tutarsızlık riski doğurur; reddedildi.

## R4 — Commission yazma tool'larının yetki kapısı

**Decision**: 043'ün Wolverine `ScopeAuthorizationMiddleware` deseni Commission.Api'ye taşınır;
yazma tool handler'ları `[RequiredScope(AuthorizationScopes.CommissionWrite)]` işaretlenir.
Yeni scope üretilmez (`commission.admin` YOK).

**Rationale**: `/mcp` mount policy'si `commission.read` — okuma için yeter ama yazma tool'unu
korumaz. 043'te `merchant.admin` gerekmişti çünkü `ecommerce-onboarding` merchant.write taşıyor;
commission tarafında admin-dışı hiçbir canlı client commission scope taşımıyor (merchant-agent
ölü ve bu işte siliniyor) → mevcut `commission.write` yeterli ayrım.

**Alternatives considered**: `commission.admin` scope — scope çoğaltma (anayasa V ruhu: scope
adı gereksiz üretilmez), gerçek bir ayrım sağlamıyor; reddedildi.

## R5 — Ölü OAuth client seed'leri sökülür

**Decision**: Identity.Server Config'ten `merchant-agent` ve `payment-agent` seed'leri silinir
(038/043 sökümlerinin artıkları).

**Rationale**: Host'ları silinmiş A2A agent'larının client'ları; kimse token almıyor ama
commission.write/payment.write taşıyan canlı kimlik bilgisi yüzeyi bırakıyor. 038 memory'sinde
"ölü payment-agent OAuth client duruyor" diye kayıtlı — bu iş doğal temizlik noktası.

**Alternatives considered**: Bırakmak — ölü yetkili kimlik yüzeyi, fail-closed ilkesine aykırı.

## R6 — Register-requests REST grubunun tamamı sökülür, redeem etkilenmez

**Decision**: `register-requests` grubu (List/Approve/Reject) tamamen silinir. Aktivasyon
redeem ucu AYRI grupta (`/merchants/activation/redeem`, Identity.Server tüketir) — dokunulmaz.

**Rationale**: 043 MCP muadillerini (`admin_get_pending_registrations`, `admin_approve/
reject_registration`) zaten üretti; REST kopyaları 043'te sayfaları silinince ölü kaldı.
Redeem sanksiyonlu S2S kanalı, admin yüzeyi değil (FR-009).

## R7 — Hassas-veri sayfası + dar BFF uçları

**Decision**: Tek Razor sayfası `Pages/Merchants/Sensitive.cshtml` (merchantId ile açılır);
Merchant.Api'de iki dar slice: `GetMerchantSensitive` (yalnız hassas 5 alan + kimlik bilgisi
adı) ve `UpdateMerchantSensitive` (yalnız hassas 5 alan). İkisi de `MerchantRead/Write` +
`AdminPlaneOnly`.

**Rationale**: Hassas alanlar admin düzleminde TEK dar yüzeyden akar; genel Update ucu
kalmadığından hassas veri genel sözleşmelerde hiç taşınmaz. Sayfaya erişim 042 admin login
düzeniyle korunur.

**Alternatives considered**: Mevcut `GetMerchant`+`UpdateMerchant`'ı ekran için tutmak — geniş
sözleşme hassas+hassas-dışı karışık taşır, "tek hassas yüzey" hedefini sulandırır; reddedildi.

## R8 — MCP tool adı sabitleri

**Decision**: Yeni tool adları `src/others/Shared/McpToolNames.cs`'e eklenir
(`MerchantAdminTools.GetMerchant/UpdateMerchant`, `CommissionAdminTools.CreatePolicy/
UpdateMargin/ChangeStatus`).

**Rationale**: 043 deseni; sabitler Shared'da, tool wrapper'ları sabitten okur.