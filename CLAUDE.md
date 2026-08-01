# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proje

DropShop: tedarikçi ürünlerini dropship modeliyle satan e-ticaret sistemi. Mimari
kurallar ECommerceWithAgentFramework projesinden devralındı: her mikroservis bir
Bounded Context, Vertical Slice + CQRS, zengin aggregate'ler, Result pattern,
Aspire + Marten + Wolverine. İlk BC: Payment (CP.VPOS sanal POS kütüphanesiyle).

## Komutlar

```bash
dotnet build                                        # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
```

- Sistemi her zaman AppHost üzerinden başlat; servisler conn-string'leri Aspire'dan alır.
- Central Package Management açık: sürümler `Directory.Packages.props`'ta. Tek istisna
  `CP.VPOS.csproj` (bilinçli CPM dışı, kendi sürümlerini tutar — dokunma).

## Yapı ve kurallar

- `src/services/Payment.Api` — Payment BC. `Domains/<Aggregate>/Features/{Commands,Queries}`
  vertical slice düzeni; bir feature = bir static class (record command + Response + Handler + endpoint).
- `src/otherProjects/CP.VPOS` — sanal POS kütüphanesi, OLDUĞU GİBİ taşındı (eski stil, nullable
  kapalı). `otherProjects` altında (versiyonlanmaz) ama Payment BC'nin aktif bağımlılığı —
  Payment.Api buradan referans verir. CP.VPOS tipleri slice sınırını GEÇMEZ: handler
  `SaleResponse`'u domain'e çevirir.
- `BankRouter` (domain service, saf hesap): komisyon + kart BIN/programı + taksit desteğine göre
  maliyet sıralı banka adayları döner. Failover: handler sıralı adayları dener; 3D'de yalnız ilk aday.
- `PosAccount` aggregate: banka POS anlaşması (credentials + taksit başına komisyon). Komisyon
  oranları buradan yönetilir; router'ın girdisidir.
- Integration event'ler `src/others/Shared` (`PaymentCompletedEvent/PaymentFailedEvent`, fanout exchange).
  Henüz tüketici yok; Order BC gelince bağlanır.
- Ortak yapı taşları `src/others/Common`'da: domain base tipleri, Result pattern, DI marker'ları,
  auth, caching, exception handler.
- Handler'lar `[Transactional]` + `IDocumentSession` (repository yok); sonuçlar
  `FeatureObjectResultModel<T>`/`ResultDomain` (exception değil).

## Bilinçli ertelemeler

- Yetkilendirme yok (Identity BC ile gelecek); endpoint'ler şimdilik korumasız.
- Test projesi henüz yok. Eklenince saf domain birim testleri olacak (`BankRouter` ilk aday);
  banka HTTP çağrıları test edilmez.
- Diğer BC'ler (Catalog, Order, Supplier...) tasarım gereği henüz yok; her biri kendi
  spec döngüsüyle eklenecek.