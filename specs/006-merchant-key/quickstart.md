# Quickstart: Merchant Key doğrulama

Elle uçtan-uca doğrulama. Proje konvansiyonu: handler/HTTP entegrasyonu quickstart ile, saf domain
birim testleriyle (`tests/Merchant.Api.Tests`) doğrulanır.

## Ön koşullar

- Sistem Aspire ile ayakta: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
  (Postgres + RabbitMQ + Merchant.Api). Tek servis izole çalıştırılmaz.
- Merchant endpoint base: `http://<merchant-api>/api/v1.0/merchants`.

## Birim testleri (saf domain)

```bash
dotnet test tests/Merchant.Api.Tests
```

Beklenen yeni testler yeşil:
- `Create` → dönen merchant'ın `MerchantKey`'i boş değil.
- Boş/whitespace merchantKey ile `Create` → `ResultDomain.Error` (presence invariant).
- `UpdateProfile` ve `Deactivate/Suspend/Activate` çağrıları sonrası `MerchantKey` değişmez.

## Senaryo 1 — Onboarding key üretir (US1)

```bash
curl -sX POST http://<merchant-api>/api/v1.0/merchants \
  -H "Content-Type: application/json" \
  -d '{"name":"Acme","email":"a@acme.com","phone":"+905551112233",
       "countryCode":"TR","cityCode":"34","mcc":"5411","webhookUrl":"https://acme.com/wh"}'
```

**Beklenen**: 200; gövde `id` + boş olmayan `merchantKey` (`mk_...`) içerir.

## Senaryo 2 — Id ile sorguda aynı key (US1 AC-2)

```bash
curl -s http://<merchant-api>/api/v1.0/merchants/{id}
```

**Beklenen**: `merchantKey` onboarding'de dönenle birebir aynı.

## Senaryo 3 — İstemci key gönderirse yok sayılır (US1 AC-4)

Create gövdesine `"merchantKey":"mk_hacker"` ekle.

**Beklenen**: Yanıt `mk_hacker` DEĞİL; sistemin ürettiği farklı bir key.

## Senaryo 4 — Benzersizlik (US1 AC-3)

İki merchant oluştur.

**Beklenen**: İki farklı `merchantKey`.

## Senaryo 5 — Key ile arama (US2)

```bash
curl -s http://<merchant-api>/api/v1.0/merchants/by-key/mk_9f1c2a7b8d3e4f5061728394a5b6c7d8
```

**Beklenen**: 200 + doğru merchant (id, temel bilgiler, status). Var olmayan key → 404 (hata değil).

## Senaryo 6 — Değişmezlik (US1 / edge)

Profil güncelle (varsa PUT) veya status değiştir, sonra Id ile tekrar sorgula.

**Beklenen**: `merchantKey` ilk değeriyle aynı.