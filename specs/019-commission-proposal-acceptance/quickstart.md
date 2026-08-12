# Quickstart: 019 Komisyon Teklifi ve Metin-Sürümlü Pazarlık

Canlı doğrulama senaryoları (Aspire + Mailpit). Ön koşul: `dotnet run --project
src/aspire/AppHost/AppHost.csproj`; banka komisyon grid'i dolu; Provisioning statüsünde bir
merchant (013 onboarding akışından) mevcut.

## S1 — Metinle teklif (US1)

1. Merchant.Agent chat: **"Kahve Dünyası'na ilk komisyon teklifimizi sun"**.
2. Bekle: agent `get_merchant` + `submit_commission_proposal` çağırır; yanıtta satır sayısı + "mail kuyruğa düştü".
3. Mailpit UI: Excel ekli mail; ekte satır no'lu tablo (banka oranı + marj); gövdede Kabul/Ret linkleri.
4. Kontrol: Excel'deki her oran = ilgili banka oranı + `DefaultMarginPoints`.

## S2 — Kabul: insansız zincir (US2)

1. Maildeki **Kabul** linki → onay sayfası → butona bas.
2. Bekle: "Teklif kabul edildi" sayfası.
3. Kontrol: Admin UI'da merchant komisyon hücreleri dolu; merchant **Active**
   (`MerchantCommissionGridReady` → `MerchantCommissionGridReadyHandler` zinciri; consumer
   log'unda "Successfully processed message", "No known handler" YOK).
4. Aynı linke tekrar git → "geçersiz bilet" sayfası, durum değişmez.

## S3 — Ret + metinle revizyon + yeniden gönder (US3)

1. (Yeni merchant ile S1 tekrarı.) Maildeki **Ret** linki → gerekçe formuna itiraz listesi yaz → gönder.
2. Agent: **"Kahve Dünyası teklifi ne durumda?"** → "Ret + gerekçe + zaman" döner.
3. Agent: **"satır 37'yi 1.85 yap"** → diff yankısı (eski→yeni) döner.
4. Agent: **"tüm 12 taksitleri 0.2 düşür"** → toplu diff döner.
5. Agent: **"Akbank 6 taksiti 0.1 yap"** (taban altı) → BÜTÜN işlem RET, ihlal satırları
   listelenir; `show_commission_draft` ile draft'ın değişmediği doğrulanır.
6. Mailpit: bu adımlar boyunca YENİ mail YOK (gönder denmedi — FR-010).
7. Agent: **"merchant'a gönder"** → yeni mail (yeni bilet); eski kabul linki artık "geçersiz bilet".

## S4 — Kabul sonrası değişmezlik (US4)

1. S2 sonrası agent: **"satır 5'i 2.0 yap"** → RET (kilitli).
2. Agent: **"yeniden teklif sun"** → RET (Accepted mevcut).

## S5 — Durum görünürlüğü (US5)

1. Agent: `show_commission_draft` + `commission_proposal_status` her fazda tutarlı.
2. Admin UI komisyon ekranı: salt-okuma grid + teklif durumu (yok/beklemede/kabul/ret+gerekçe+zaman);
   Finalize butonu YOK.

## Hata senaryoları

- Banka grid'i boşken teklif → agent hata mesajı (kombinasyon yok).
- E-postasız merchant'a teklif → agent hata mesajı (iletişim adresi eksik).
- TTL dolmuş bilet (config'te kısa TTL ile test) → linkler etkisiz.

## Birim test doğrulaması

`dotnet test tests/Commission.Api.Tests` — draft üretimi (marj/sıra/satır no), revizyon
(set/delta/taban bekçisi/bütün-veya-hiç), proposal durum makinesi (bilet TTL/tek kullanım/
Supersede), kilit kuralları yeşil.