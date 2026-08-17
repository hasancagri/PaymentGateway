# Research: Ödeme Süreci A2A + MCP Üzerinden (038)

**Date**: 2026-08-16 · **Spec**: [spec.md](spec.md)

Tüm NEEDS CLARIFICATION spec aşamasında kapatıldı (Q1=A, Q2=B, Q3=A). Bu belge teknik
bilinmeyenlerin kararlarını toplar; her karar mevcut kod taramasına dayanır.

## R1 — Devir kanalı: A2A → Payment.Agent (007 dirilişi)

- **Decision**: ChatAgent ödeme isteğini A2A (JSON-RPC) ile PG Payment.Agent'a gönderir;
  Payment.Agent /mcp tool'larını sırayla çağırır. ChatAgent PG /mcp'ye doğrudan BAĞLANMAZ.
- **Rationale**: Kullanıcı kararı. Ajans ayrımı: PG süreci PG'nin agent'ı yönetir; EC yalnız
  yorumlanmış istek gönderir. Altyapı hazır: EC'de `A2APayment` sabitleri
  (`payment-gateway-agent`, `a2a-payment` named client, `PaymentGateway:A2AUrl`), PG'de
  A2A host + AgentCard + `AgentTokenHandler` (019/024'te canlı çalıştı).
- **Alternatives considered**: ChatAgent → PG /mcp doğrudan (032 onboarding deseni) —
  reddedildi: PG tarafında agent yorumu olmaz, kullanıcı iki-ajanlı kurguyu seçti.

## R2 — Kart referansı: vault token (mevcut sözleşme)

- **Decision**: A2A isteği kartı **vault token** ile refere eder (StoredCard'ın opak
  token'ı — `ChargePayment.ChargePaymentCommand.VaultToken` mevcut sözleşmesi). Kart
  listeleme tool'u da kart başına vault token + maskeli görüntü alanları döner.
- **Rationale**: 031/032/033 zinciri bu referansı kurdu; EC Customer.Api wallet'ı kart
  başına vault token'ı zaten saklıyor (`get_default_card_bin` bunun üstünde). PAN/CVC ve
  iyzico `cardUserKey`/`cardToken` PG içinde kalır, dışarı SIZMAZ — vault token opak ve
  yalnız PG'de çözülür.
- **Alternatives considered**: StoredCard Guid Id ile refere — EC wallet Id değil token
  tutuyor; yeni eşleme sütunu gerektirirdi (YAGNI). iyzico cardToken doğrudan — sağlayıcı
  sırrı dışarı sızar, RET.

## R3 — Çekim bağlamı: buyer GERÇEK (EC'den, A2A'da verbatim), sepet SENTETİK (gateway'de)

**GÜNCELLENDİ (implement sırasında, kullanıcı kararları 2026-08-16)**

- **Decision**: (a) **Sepet kalemleri A2A/MCP isteğinde TAŞINMAZ** — iyzico'nun zorunlu
  `basketItems` alanını gateway TEK SENTETİK KALEMLE sentezler (`IyzicoRequestOptions`:
  BasketItemId/Name/Category; price = istek tutarı; 033 EC köprüsü de zaten tek sentetik
  kalem gönderiyordu). (b) **Buyer GERÇEK müşteri bilgisidir**: Customer.Api'ye YENİ
  `get_payment_context` Agent tool'u — seçilen/varsayılan kartın vault token'ı + buyer
  (profil ad/soyad/e-posta/GSM + AddressBook VARSAYILAN adresi; varsayılan adres yoksa
  NotFound → asistan önce adres eklettirir). TCKN/ülke/IP e-ticarette tutulmadığından 033
  sabitleri korunur (sandbox kabulü). ChatAgent buyer'ı A2A isteğine OLDUĞU GİBİ (verbatim)
  koyar; persona yönergesi buyer alanlarını ve vault token'ı kullanıcıya GÖSTERMEMEYİ ve
  DEĞİŞTİRMEMEYİ emreder.
- **Rationale**: Kullanıcı kararları — "sepet PG'ye taşınmaz; sadece tutar yeter" (sepet
  sentezi) + "alıcı, ECommerce'de alışveriş yapanın gerçek bilgisi olsun; adres de dahil"
  (buyer gerçek). PII (ad/adres) LLM bağlamından verbatim geçer — bilinçli kabul, sandbox.
- **Alternatives considered**: (a) PG'nin buyer'ı EC'den HTTP ile çekmesi — BC→BC senkron
  bağ, RET. (b) Buyer'sız/tam-sentetik buyer çekimi — önce değerlendirildi, kullanıcı
  gerçek müşteri bilgisini istedi, RET. (c) Sepet kalemlerini A2A'da taşımak — kullanıcı
  "sadece tutar" dedi, RET.

## R4 — Merchant statü kapısı: Payment BC'de event-fed referans

- **Decision**: Payment BC `merchant.lifecycle` fanout'unu (mevcut Shared kontratlar:
  `MerchantCreated`/`MerchantStatusChanged`, 012) dinler; `MerchantStatusReference`
  dokümanına (Marten, Payment DB) idempotent upsert yapar (Identity.Server
  `MerchantClientEventHandler` şablonu). `ChargeSavedCardForAgent` çekimden önce bu
  referanstan statüyü okur: kayıt yok VEYA statü ≠ Active → fail-closed RET
  (`ResultDomain` hatası, sağlayıcıya gidilmez).
- **Rationale**: /mcp bacağı makine token'ı taşır (statü bilgisi yok); anayasa "charge
  yalnız Active, fail-closed" der. HTTP uçlarındaki `payment.charge` scope kapısı A2A/MCP
  yolunda yok — kapı gateway İÇİNE alınır. Event-fed read model 010 (Reference.Api) ve 012
  (Identity tüketimi) emsalleriyle yerleşik desen; BC izolasyonu korunur.
- **Alternatives considered**: Her çekimde Merchant.Api'ye HTTP sorgusu — çapraz-BC senkron
  bağımlılık + gecikme, RET. Statüyü A2A isteğinde taşımak — çağıranın beyanına güvenmek
  fail-open olur, RET.
- **Tuzak notu**: Wolverine tüketici sınıf adı TEKİL "Handler" bitmeli
  (`MerchantLifecycleEventHandler`); çoğul "Handlers" 6.4'te sessizce keşfedilmiyor.
  Message store yok → `ProcessInline` + RabbitMQ redelivery (Identity emsali).

## R5 — /mcp yüzeyi ve kimlik: 011 deseni, tek policy

- **Decision**: Payment.Api `/mcp` ucu geri kurulur; tek policy `payment.write`
  (011'deki sökülen desenin aynısı). Payment.Agent mevcut `AgentTokenHandler`'ıyla
  (client_credentials, −30 sn yenileme) çağırır. Identity seed'inde `payment-agent`
  istemcisi zaten var; scope kontrolü `ScopeClaimArrayHandler` üzerinden.
- **Rationale**: Kullanıcı kararı Q2=B — A2A bacağı kimliksiz; tek yetki katmanı /mcp
  makine token'ı. Statü kapısı R4 ile ayrı sağlanır.
- **Alternatives considered**: A2A'da merchantKey/merchant token — ertelendi (Q2), ayrı
  auth işi.

## R6 — MCP tool seti ve Payment.Agent skill'leri

- **Decision**: PG /mcp İKİ tool sunar (kontrat: contracts/mcp-payment-tools.md):
  `get_installment_options` (vault token + tutar → taksit seçenekleri),
  `charge_saved_card` (vault token + tutar + taksit + paidPrice + buyer + sepet kalemleri →
  çekim). KART TOOL'U YOK (kullanıcı kararı): kart listesi/seçim/varsayılan ECommerce
  cüzdanında çözülür (mevcut `get_cards`/wallet araçları); `StoredCard`'da müşteri alanı
  olmadığından (yalnız MerchantId) merchant-geneli liste tüm müşterilerin kartını döker —
  gateway'e kart yüzeyi açılmaz. Payment.Agent AgentCard'ına `charge_saved_card` skill'i
  eklenir; `installment_quote` (BIN, 024) AYNEN KALIR — EC'nin quote-only akışı kırılmaz.
  RouterInstructions genişler; 007 kuralı sürer: LLM tutar/kart/taksit ÜRETMEZ, yalnız tool
  sırası kurar.
- **Rationale**: İki tool US1/US2'ye birebir; US3 (kart seçimi) tamamen EC orkestrasyonu —
  seçilen kartın vault token'ı A2A isteğinde gelir, PG tarafında kod değişikliği gerektirmez.
  Her tool tek ForAgent slice çağırır (015/016 kuralları). Var olan HTTP slice'ları
  (ChargePayment, InstallmentOptions) DURUR — Agent slice'ları kendi kopyasını taşır (kod
  tekrarı bilinçli, Agent slice Commands/Queries'e gidemez kuralı).
- **Alternatives considered**: Agent slice'ın mevcut Command'ı `IMessageBus` ile çağırması —
  015 kuralı açıkça yasaklıyor, RET.

## R7 — ECommerce sökümü ve persona güncellemesi (Q1=A)

- **Decision**: Customer.Api'den `SavedCardPaymentMcpTools` (get_card_installments,
  charge_default_card) + ilgili Agents slice'ları + PG'ye HTTP çekim köprüsü SÖKÜLÜR.
  Yerine `get_payment_context` tool'u gelir (R3). ChatAgent assistant persona 8/9 kuralları
  yeniden yazılır: taksit = get_basket → get_payment_context → A2A `installment_quote`
  yerine token'lı sorgu için A2A text mesajı; çekim = açık onay → A2A çekim isteği.
  `get_default_card_bin` KALIR (BIN-quote akışı 024 yaşıyor); cüzdanın kart listeleme
  aracı (`list_cards`) KALIR ve US3'ün kart seçimi bunun üstünden yürür — `get_payment_context`
  varsayılan ya da SEÇİLEN kartın vault token'ını döner. ChatAgent'taki
  `CustomerTools.GetCardInstallments/ChargeDefaultCard` sabitleri ve tool kayıtları çıkar.
- **Rationale**: Kullanıcı kararı Q1=A — tek yol A2A; çift yüzey yaşamaz.
- **Alternatives considered**: Paralel geçiş (Q1=B) — reddedildi (kullanıcı).

## R8 — Payment.Agent LLM anahtarı ve konfig

- **Decision**: Payment.Agent'ın LLM router'ı mevcut düzeni korur: chat anahtarı agent
  config'i (`OpenAI:ApiKey`/user-secrets); /mcp adresi + Identity adresi Options POCO'larıyla
  bağlanır (magic-string config yasak — `AddOptionsExt` deseni). Yeni config bölümleri
  DataAnnotations'lı POCO'lar olarak eklenir.
- **Rationale**: Mevcut kural seti (config Options pattern, feedback memory'leri).
- **Alternatives considered**: Yok — kural sabit.