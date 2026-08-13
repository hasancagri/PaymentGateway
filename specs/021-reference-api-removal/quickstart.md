# Quickstart: Reference.Api Removal (021)

**Date**: 2026-08-13 | **Plan**: [plan.md](plan.md)

Ön koşul: .NET 10 SDK; canlı senaryolar için Docker (Aspire: Postgres + RabbitMQ).
Dev veritabanları sıfırlanır (migration yok — proje pratiği).

## S1 — Derleme ve kalıntı taraması (SC-001, SC-004)

```bash
dotnet build
grep -rniE "ReferenceDataUpdated|ReferenceBank|ReferenceCountry|ReferenceCity|ReferenceMcc|reference-api|referenceDb|Reference\.Api" \
  src tests PaymentGateway.slnx --include="*.cs" --include="*.csproj" --include="*.cshtml" --include="*.slnx" | grep -v "specs/"
```

**Beklenen**: Build 0 hata; tarama 0 satır (yalnız spec artefaktlarında iz kalır).
Çözümde `Reference.Api` ve `Reference.Api.Tests` projeleri yoktur.

## S2 — Testler yeşil (SC-005)

```bash
dotnet test
```

**Beklenen**: 5 test derlemesi (Payment 81, Merchant 50, Commission 64, Iyzipay 10 —
Reference'ın 21'i silindi); tümü yeşil.

## S3 — Sistem Reference olmadan ayağa kalkar (SC-002)

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

**Beklenen**: Dashboard'da `reference-api` ve `referenceDb` YOK; kalan servisler
(payment-api, merchant-api, commission-api, admin, identity, agent'lar, mail-worker,
mailpit) sağlıklı. Merchant/Commission loglarında `reference.data-updated` exchange/queue
deklarasyonu geçmez, "No known handler" uyarısı yoktur.

## S4 — Katalogsuz banka tanımı (SC-003, US2)

Admin → Bankalar → Yeni: **Kod + Ad elle girilir** (dropdown yok), taksit seçimi aynı.

**Beklenen**: Banka oluşur (ör. kod `0064`, ad `İş Bankası`); aynı kodla ikinci deneme
duplicate hatası verir (kod benzersizliği korunur). Banka listesinde ad görünür.

## S5 — Katalogsuz settlement hesabı (SC-003, US2)

Admin → Settlement Hesapları → Yeni: **BankCode elle girilir** (dropdown yok), IBAN + hesap
sahibi girilir.

**Beklenen**: Geçerli IBAN ile hesap oluşur; bozuk IBAN mod-97'den döner (kural korunmuş);
aynı IBAN ikinci kez duplicate reddi alır. Liste/detay `BankCode` gösterir (`BankName` yok).

## S6 — Merchant sorguları zenginleştirmesiz (US2)

```bash
# token al (admin-ui client) ve merchant çek — veya Admin ekranından merchant detayına bak
```

**Beklenen**: Merchant tekil/anahtarla/agent (`get_merchant` MCP) yanıtlarında Country/City/
MCC ad alanları yok; kod alanları duruyor; hata yok. 019 komisyon teklif akışı (Agent Chat →
`show_commission_draft`) banka adlarını Commission'ın kendi Bank kayıtlarından gösterir.

## Notlar

- **Doğrulama sonucu (2026-08-13, implement)**: S1 GEÇTİ (build 0 hata; kalıntı taraması 0 —
  tek kalan eşleşme alakasız yerel `src/reference-architecture.md` dosya adı). S2 GEÇTİ
  (201 test: Payment 81, Commission 64, Merchant 46, Iyzipay 10; Merchant 50→46 —
  ReferenceKeyTests read-model'le birlikte silindi). **S3-S6 canlı senaryolar YAPILMADI** —
  kullanıcı kararı ("uygulamanın düzgün çalışıp çalışmadığına şu an bakmıyorum").
- **Kapsam genişlemesi (kullanıcı, implement sırasında)**: Merchant.Api'nin TÜM Features
  slice'ları + endpoint'ler + MCP yüzeyi silindi (023 SubMerchant yeniden kuracak).
  Admin merchant/settlement/banka ekranları ve Merchant.Agent skill'leri derlenir ama
  uçları olmadığından çalışmaz — bilinçli ara durum.
- Broker'da eski `reference.data-updated` exchange kalıntısı görülürse dev volume sıfırlaması
  yeterli (koddan deklarasyon kalktı).