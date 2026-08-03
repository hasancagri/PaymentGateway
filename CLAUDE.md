# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Proje

DropShop: tedarikçi ürünlerini dropship modeliyle satan e-ticaret sistemi. Mimari
kurallar ECommerceWithAgentFramework projesinden devralındı: her mikroservis bir
Bounded Context, Vertical Slice + CQRS, zengin aggregate'ler, Result pattern,
Aspire + Marten + Wolverine.

Mevcut BC'ler: **Payment** (CP.VPOS sanal POS), **Merchant** (onboarding + settlement
hesapları), **Commission** (banka referansı + komisyon grid). Ayrıca **Admin** (Razor Pages
BFF) iki API'yi service discovery ile tüketir. Her BC kendi spec döngüsüyle (spec-kit,
`specs/<NNN>/`) eklendi.

## Komutlar

```bash
dotnet build                                        # tüm çözüm (PaymentGateway.slnx)
dotnet run --project src/aspire/AppHost/AppHost.csproj   # sistemi Aspire ile başlat (Postgres + RabbitMQ)
dotnet test tests/Merchant.Api.Tests                # saf domain birim testleri (Merchant)
dotnet test tests/Commission.Api.Tests              # saf domain birim testleri (Commission)
```

- Sistemi her zaman AppHost üzerinden başlat; servisler conn-string'leri Aspire'dan alır.
- Central Package Management açık: sürümler `Directory.Packages.props`'ta. Tek istisna
  `CP.VPOS.csproj` (bilinçli CPM dışı, kendi sürümlerini tutar — dokunma).

## Yapı ve kurallar

- `src/services/Payment.Api` — Payment BC. `Domains/<Aggregate>/Features/{Commands,Queries}`
  vertical slice düzeni; bir feature = bir static class (record command + Response + Handler + endpoint).
- `src/services/Merchant.Api` — Merchant BC. `Merchant` aggregate (onboarding) + `MerchantSettlementAccount`
  aggregate (payout banka hesabı; IBAN mod-97 saf doğrulama, yerel `BankCatalog` kopyası, endpoint'ler
  merchant-scoped `merchants/{merchantId}/settlement-accounts`). Aynı vertical slice deseni.
- `src/services/Commission.Api` — Commission BC. `Bank` aggregate (katalogdan kod+ad) + banka/merchant
  komisyon grid'leri (kombinasyon-bazlı, atomik toplu upsert). Banka seed yok.
- `src/agents/Payment.Agent` — A2A host + LLM router + MCP client (007). Payment BC **DEĞİL** —
  kalıcılık yok, stateless delivery adaptörü. `AddA2AServer(agent)` + `MapA2AJsonRpc` +
  `MapWellKnownAgentCard` (`/.well-known/agent-card.json`). LLM yalnız tool sırasını kurar
  (`get_installment_options` → `select_installment`); tutar/banka/kart üretmez (domain'den).
  Chat anahtarı agent config'inden (`OpenAI:ApiKey`/user-secrets). Tüm A2A/Agent Framework paketleri
  preview — `Directory.Packages.props`'ta pin.
- Payment.Api MCP yüzeyi (007): agent'a açık işlemler `Domains/PaymentSessions/Features/**Agent**/`
  altında (Commands/Queries değil); MCP tool'ları `PaymentSessionMcpTools.cs`'te her tool ayrı
  `[McpServerToolType]`, yalnız `IMessageBus.InvokeAsync` ile slice'ı sarar. Kayıt:
  `AddMcpServer().WithToolsFromAssembly()` + `MapMcp("/mcp")`. `PaymentSession` aggregate = A2A
  akışının kalıcı izdüşümü (faz: Opened→QuoteProvided→InstallmentSelected/Failed). Token→BIN çözümü
  `CardVault/ICardVault` (`SimulatedCardVault` → 008 `ResolveBinCard`; PAN kanala girmez). Model A
  quote = kullanıcı tutarı sepet tutarı (komisyon yalnız `BankRouter` POS seçiminde). Çekim 007 dışı.
- `src/ui/Admin` — Razor Pages BFF (yetki yok). Merchant/Bank/komisyon/settlement ekranları; typed
  `HttpClient`'lar Aspire service discovery ile API'leri çağırır (`http://merchant-api` vb.). Backend'e
  kural sızdırmaz — yalnız API sonucunu (`ApiResult`/`MessageText` Türkçe) gösterir.
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

- Yetkilendirme yok (Identity BC ile gelecek); endpoint'ler ve Admin BFF şimdilik korumasız.
- Test: saf domain birim testleri var (`tests/Merchant.Api.Tests`, `tests/Commission.Api.Tests`).
  Handler/HTTP/Razor Pages entegrasyonu test edilmez — quickstart senaryolarıyla elle doğrulanır.
- Diğer BC'ler (Catalog, Order, Supplier...) tasarım gereği henüz yok; her biri kendi
  spec döngüsüyle eklenecek.