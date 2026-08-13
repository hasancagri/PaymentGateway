# Implementation Plan: Iyzico Payment Channel — Yapısal Eritme

**Branch**: `022-iyzico-payment-channel` | **Date**: 2026-08-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/022-iyzico-payment-channel/spec.md`

## Summary

Legacy POS ekseni (CP.VPOS, BankRouter, PosAccount, BinCard) ve dört Iyzipay* projesi
sistemden çıkar; kullanılan iyzico istemci yapıları üç BC'nin boşaltılmış `Domains/`
alanlarına sorumluluğa göre dağıtılır (ödeme/taksit/saklı kart → Payment; SubMerchant →
Merchant; payout/işlem raporları → Commission), çekirdek istemci altyapısı BC başına
`Provider/` kopyası olur (CardVault emsali). Kullanılmayan SDK modülleri silinir. Ölü BC
test projeleri kaldırılır. Hedef: çözüm derlenir + kalıntı 0 — çalışan akış ve canlı
doğrulama kapsam dışı (kullanıcı kararı).

## Technical Context

**Language/Version**: C# / .NET 10 (proje sayısı 5 azalır: CP.VPOS + 4 Iyzipay*; yeni proje yok)

**Primary Dependencies**: Newtonsoft.Json (taşınan istemci kodu kullanır — BC csproj'larına
sürümsüz `PackageReference` olarak girer, sürüm CPM'de zaten tanımlı 13.0.4); Marten/Wolverine
kayıtları yalnız TEMİZLENİR

**Storage**: Değişmez; silinen aggregate'lerin dokümanları dev DB sıfırlamasıyla gider

**Testing**: 5 test projesi silinir (3 BC + 2 Iyzipay) — ölü aggregate/SDK testleri.
tests/ boşalır; 023/024 yeni domain testleri getirir. Kapanış doğrulaması derleme + tarama

**Target Platform**: Mevcut Aspire orkestrasyonu (değişiklik yalnız Payment/Merchant/
Commission içi; AppHost'a dokunulmaz — CP.VPOS AppHost'ta yok)

**Project Type**: Söküm + kod dağıtımı (yapısal refactor; davranış/akış hedefi yok)

**Performance Goals**: N/A

**Constraints**: Sağlayıcı tipleri BC dışına sızmaz (FR-004); CardVault PAN-koruma
çekirdeği korunur (FR-006); depoya sır girmez (FR-007); kırık/yarım kod bırakılmaz (FR-005)

**Scale/Scope**: ~90 SDK dosyası dağıtılır/yeniden-namespace'lenir, ~60 SDK dosyası +
5 proje + 3 BC'nin Program.cs/GlobalUsings onarımı; CLAUDE.md büyük güncelleme

## Constitution Check

*GATE: Anayasa v1.4.0'a göre değerlendirildi (Phase 0 öncesi + Phase 1 sonrası).*

| İlke | Değerlendirme | Sonuç |
|------|---------------|-------|
| I. BC İzolasyonu | Sağlayıcı istemcisi BC başına kopya (`Provider/`) — paylaşılan kütüphane adası kurulmaz; hiçbir BC diğerinin tipine referans vermez | PASS |
| II. Zengin Domain | Bu fazda aggregate YOK — taşınan tipler davranışsız istemci modelleri; zengin aggregate'ler 023/024'te bu malzemeden doğar | JUSTIFIED (aşağıda) |
| III. Vertical Slice + CQRS | Slice eklenmez; ölü slice bırakılmaz (hepsi kullanıcı tarafından silindi) | PASS |
| IV. Result Pattern | Handler yok — uygulanacak yüzey yok | PASS |
| V. Merkezi Kimlik/Yetki | Endpoint kalmıyor (Payment/Merchant/Commission uçsuz ara durum); açıkta korumasız uç yok. Identity/Admin/agent auth altyapısı dokunulmaz | PASS |
| VI. Spec-Driven | Tam akış izleniyor; kapsam değişiklikleri spec'e amendment olarak işlendi | PASS |
| Teknoloji: CPM | Iyzipay CPM-dışı adası ÖLÜR — taşınan kod BC projelerine girer, Newtonsoft sürümü CPM'den (13.0.4; SDK 13.0.2 pini kalkar). CPM istisnası yalnız CP.VPOS'tu, o da siliniyor → istisnasız CPM | PASS (iyileşme) |
| Teknoloji: Nullable/ImplicitUsings | Taşınan dosyalar BC projelerine girer (ikisi de AÇIK) — derleme uyarı üretebilir, hata üretirse asgari düzeltme | PASS (izleme) |
| Aggregate-klasör kuralı (CLAUDE.md) | `Domains/<Alan>/` bu fazda AggregateRoot içermez | JUSTIFIED (aşağıda) |

**Post-Phase-1 yeniden değerlendirme**: Yeni ihlal yok; iki JUSTIFIED madde Complexity
Tracking'de. GEÇTİ.

## Project Structure

### Documentation (this feature)

```text
specs/022-iyzico-payment-channel/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 (R1-R8: envanter + dağıtım haritası + kararlar)
├── data-model.md        # Phase 1 (SDK → BC dosya eşlemesi)
├── quickstart.md        # Phase 1 (derleme + tarama doğrulaması)
└── tasks.md             # Phase 2 (/speckit-tasks)
```

`contracts/` ÜRETİLMEDİ: dışa yeni arayüz yok; mevcut uçlar zaten kullanıcı silmesiyle
kalktı, yenisi bu fazda açılmıyor.

### Source Code (repository root)

```text
# SİLİNEN PROJELER (slnx + disk):
src/services/CP.VPOS/                 (+ .gitignore satırı, Payment.Api referansı)
src/services/Iyzipay/                 (dağıtım sonrası kalan her şey)
src/services/Iyzipay.Samples/
tests/Iyzipay.Tests/
tests/Iyzipay.Tests.Functional/
tests/Payment.Api.Tests/  tests/Merchant.Api.Tests/  tests/Commission.Api.Tests/

# DAĞITIM (research R2 haritası; namespace'ler BC'ye ait, "Iyzipay" adı yaşamaz):
src/services/Payment.Api/
├── Provider/                         # çekirdek istemci kopyası (Options, HttpClient, hash, json…)
├── Domains/Payments/                 # ödeme + iptal/iade model & istekleri
├── Domains/Installments/             # taksit + BIN sorgu tipleri
├── Domains/StoredCards/              # kart saklama tipleri (CardVault/ aynen durur)
src/services/Merchant.Api/
├── Provider/                         # çekirdek kopya (mini)
└── Domains/SubMerchants/             # SubMerchant model & istekleri
src/services/Commission.Api/
├── Provider/                         # çekirdek kopya (mini + V2 zarfları)
├── Domains/Payouts/                  # payout/cross-booking tipleri
└── Domains/TransactionReports/       # V2 işlem raporu tipleri

# ONARILAN:
src/services/{Payment,Merchant,Commission}.Api/{Program.cs,GlobalUsings.cs,csproj}
src/services/Payment.Api/CardVault/   # kırık BinCard/CardInfo referansı varsa vault-içi tanım
src/services/Merchant.Api/ReadModels/ # MerchantCommissionGridReadyHandler ölü aggregate'e bakıyorsa silinir
PaymentGateway.slnx                   # 5 proje çıkar
.gitignore                            # CP.VPOS satırı çıkar
CLAUDE.md                             # 022 sonrası yapı anlatımı
```

**Structure Decision**: Dağıtım kullanıcının boşalttığı `Domains/` alanlarına yapılır;
çekirdek istemci `Provider/` klasöründe (Domains dışı teknik klasör — `CardVault/`
emsali). BC başına kopya, paylaşım yok (Anayasa I).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `Domains/` klasörleri bu fazda AggregateRoot içermiyor (CLAUDE.md aggregate-klasör kuralı askıda) | Yapısal ara faz: kullanıcı eski aggregate'leri sildi, SDK malzemesi yerleştiriliyor; gerçek aggregate'ler 023 (SubMerchant/Merchant) ve 024 (komisyon) speclerinde bu malzemeden şekillenecek | Malzemeyi Domains dışında tutmak — kullanıcı talimatı açık ("Iyzico içerisindeki yapıları buralara ekleyeceğiz"); ikinci kez taşıma israfı |
| Zengin-domain ilkesi bu fazda uygulanamıyor (davranışsız istemci tipleri) | Aynı gerekçe — bu spec'in çıktısı domain değil, doğru yere yerleşmiş sağlayıcı malzemesi | Davranış uydurmak (sahte aggregate sarmak) — YAGNI ve yanıltıcı |