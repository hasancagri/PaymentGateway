# Research: Kart Vault Dirilişi (031)

**Date**: 2026-08-14 | **Spec**: [spec.md](spec.md)

## R1 — Kaynak: 017 kodu git tarihçesinden, kanıtlanmış hâliyle

**Decision**: Söküm öncesi durum `9c393ad^`'de eksiksiz: `StoredCard` aggregate (Luhn + MM/yy
expiry + türetimler + soft `Revoke` idempotent), `TokenizeCard`/`RevokeCard` slice'ları,
`CardVault/` altyapısı (`IPanProtector`, `DevPanProtector` AES, `PanTools`: LuhnValidator/
BinExtractor/Last4Extractor/BrandDetector), eski birim testleri. Dirilişte bu kod TEMEL alınır;
sapmalar R2-R4'te.

**Rationale**: 017 canlı E2E doğrulanmıştı (PR #26); kanıtlanmış kodu yeniden icat etmek risk.

## R2 — SharedKernel bağımlılığı kırılır: BC-içi `CardBrand`

**Decision**: Eski `SharedKernel.CardTaxonomy.CardBrand` (021'de silindi) yerine
`Domains/StoredCards/CardBrand.cs` düz enum'u: `Unknown=0, Visa, MasterCard, Amex, Troy`.
`BrandDetector` prefix kuralları aynen (4→Visa, 34/37→Amex, 9792→Troy, 51-55 & 2221-2720→MC).

**Rationale**: Aggregate klasörü enum istisnası; cross-BC taksonomi ihtiyacı yok (yalnız
gösterim/denetim alanı).

## R3 — YAGNI kırpma: `UpdateCard` ve `UpdateDetails` geri gelmez

**Decision**: Eski `UpdateCard` slice'ı + `StoredCard.UpdateDetails` metodu diriltilmez —
ECommerce kart düzenleme ekranı yok, canlı istemci yalnız Tokenize + Revoke çağırıyor (Explore
keşfi: `GatewayCardTokenizer.TokenizeAsync/RevokeAsync`). `ICardVault`/resolve tarafı da (ödeme
akışının parçası) bu kapsama girmez — ödeme spec'inde gelir.

**Rationale**: Spec Assumption; ölü uç taşımak bakım yükü.

## R4 — Dış sözleşme CANLI istemciden sabitlendi (doğrulanmış)

**Decision**: `GatewayCardTokenizer` kaynak okumasıyla sözleşme birebir:
- `POST {base}/api/v1.0/merchants/{merchantId}/vault/cards` gövde `{ pan, expiry: "MM/yy",
  holderName }` (CVV GÖNDERİLMİYOR; ECommerce holder'ı sabit "CARD HOLDER" yolluyor) → 200 +
  `{ token }` (istemci yalnız `token` alanını okuyor)
- `DELETE .../vault/cards/{token}` → 2xx başarı; istemci gövde okumuyor (fail-open)
- Auth: Bearer (client_credentials, `cards.write`), istemci `MerchantTokenProvider` merchantId+
  MerchantKey ile 033'te kaydedilen kimlikten token alıyor
Eski endpoint'ler bu sözleşmeyi zaten üretiyordu → route/gövde/yanıt AYNEN korunur.

**Rationale**: FR-009 sıfır-dokunuş ancak sözleşme birebirse sağlanır; iki taraf da tarihte
birlikte canlı doğrulanmıştı.

## R5 — PAN koruması: `DevPanProtector` aynen (dev kabulü)

**Decision**: AES-CBC, SHA256'dan türetilmiş DEV-SABİT anahtar, IV cipher'a prepend — eski kod
aynen. Yalnız `Protect` (write-only PAN); `Reveal` ödeme spec'ine kalır. Prod KMS/HSM notu yorum +
spec Assumption olarak durur.

**Rationale**: 017'nin bilinçli dev kararı; kapsamda değişiklik yok.

## R6 — Konumlar: `CardVault/` altyapı klasörü geri gelir

**Decision**: `IPanProtector` + `DevPanProtector` + `PanTools` eski yerinde:
`src/services/Payment.Api/CardVault/` (aggregate DEĞİL, altyapı — private helper serbest; 017'de
aynı anayasayla kabul edilmişti). `DevPanProtector : ISingletonDependency` marker'ıyla
`AddAllDependencies` otomatik kaydına girer (eski desen).

**Rationale**: 015 "teknik klasör yasağı" feature'lar içindir; bu, seam'li altyapı (protector
prod'da değişecek). Emsal: 017 aynı yapıyla yaşadı.

## R7 — Program.cs dokunuşu tek satır

**Decision**: Payment.Api Program.cs'te Marten/versioning/auth (CardsWrite dahil) ve
`apiVersionSet` ZATEN hazır (canlı doğrulama: dosya bugünkü hali) — tek eksik
`app.AddStoredCardGroupEndpointExtension(apiVersionSet);`. Marten şema kaydı gerekmiyor (mevcut
stil: Schema.For çağrısı yok). StoredCard identity: `Token` (string) — Marten `Identity` konvansiyonu
eski kodda `Token` property'siyle çalışıyordu (LoadAsync<StoredCard>(token)); aynen.

## R8 — Test: eski 3 test dosyasından 2'si döner

**Decision**: `tests/Payment.Api.Tests` YOK (022'de silindi) — yeni proje açılır (Merchant/
Commission test csproj deseni + slnx kaydı). Eski `StoredCardCreateTests` + `StoredCardRevokeTests`
uyarlanır (UpdateTests R3 gereği gelmez); ek: BrandDetector/Luhn matrisi, normalize (boşluk/tire),
12-19 hane sınırları.

**Rationale**: R9 test politikası (saf domain); eski testler hazır şablon.
