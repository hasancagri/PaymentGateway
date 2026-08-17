# Quickstart: Ödeme Süreci A2A + MCP Üzerinden (038)

Canlı doğrulama rehberi — sandbox-only kural gereği tüm senaryolar iyzico sandbox test
kartlarıyla koşulur. Sözleşme ayrıntıları: [contracts/](contracts/) · Varlıklar:
[data-model.md](data-model.md)

## Önkoşullar

1. İki repo da AppHost ile ayakta (İKİŞER DEĞİL — çift-AppHost tuzağına dikkat):
   ```bash
   dotnet run --project src/aspire/AppHost/AppHost.csproj   # PaymentGateway
   dotnet run --project src/aspire/AppHost/AppHost.csproj   # ECommerceWithAgentFramework (kendi kökünden)
   ```
2. ECommerce merchant'ı gateway'de **Active** (onboarding tamam, 029/032 akışı).
3. Test müşterisinde kayıtlı kart(lar) var — ekran yolundan eklenmiş (chat'ten kart ekleme
   YOK): çok taksit için İş Maximum `5406670000000009`, tek çekim denemesi için Halkbank
   `552879...`.
4. Payment.Agent LLM anahtarı config'te (`OpenAI:ApiKey` user-secrets).
5. RabbitMQ'da `merchant.lifecycle` akıyor — Payment.Api log'unda
   "Successfully processed message" görülmeli ("No known handler" ASLA — tekil "Handler"
   adlandırma tuzağı).

## S1 — Taksit sorgusu (US1)

ECommerce chat (giriş yapmış müşteri): sepete ürün ekle, sonra:

> "Sepetim için taksit seçeneklerini göster"

**Beklenen**: ChatAgent sepet toplamını alır, kart token'ını cüzdandan çözer, A2A
`installments` isteği gönderir; Payment.Agent `get_installment_options` tool'unu çağırır.
Müşteriye taksit listesi (taksit sayısı + toplam tutar) gelir. EC Customer.Api log'unda
PG'ye HTTP çekim çağrısı YOK (köprü söküldü).

## S2 — Çekim (US2)

S1 devamında:

> "3 taksitle öde"

**Beklenen**: ChatAgent açık onay ister ("X TL'yi 3 taksitle... onaylıyor musunuz?").
Onay sonrası A2A `charge` isteği → `charge_saved_card` → iyzico sandbox çekimi.
Müşteriye ödeme numarası + durum döner; PG Payment DB'de Succeeded kayıt; iyzico sandbox
panelinde işlem görünür (tutar eşleşir).

## S3 — Statü kapısı (fail-closed)

Admin ekranından merchant'ı **Passive** yap; S2'yi tekrar dene.

**Beklenen**: Çekim gateway İÇİNDE reddedilir (iyzico'ya istek GİTMEZ — PG log'unda
sağlayıcı çağrısı yok); müşteriye "ödeme alınamadı" tarzı mesaj. Merchant'ı Active'e geri
al, S2 yeniden geçer (event-fed statü referansının güncellendiğinin kanıtı).

## S4 — Kart seçimi (US3)

İki kartlı müşteriyle:

> "Kartlarımı göster" → liste EC cüzdanından gelir (maskeli)
> "İkinci kartımla taksitleri göster" → seçilen kartın token'ı A2A'ya gider

**Beklenen**: Taksit yanıtı seçilen karta ait (farklı banka → farklı seçenek seti);
gateway'e kart listeleme isteği hiç gitmez (PG log'unda yalnız installments çağrısı).

## S5 — Güvenlik denetimleri

- ChatAgent'a "kart eklemek istiyorum" de → chat'ten kart ekleme REDDEDİLİR, ekran yoluna
  yönlendirir (PAN agent bağlamına girmez).
- S1/S2 yanıt gövdelerinde PAN/CVC/cardUserKey/cardToken ARANIR — hiçbirinde olmamalı
  (SC-003).
- Payment.Api /mcp'ye token'sız istek → 401 (payment.write policy).

## Başarı ölçütleri eşlemesi

| Senaryo | Spec kriteri |
|---------|--------------|
| S1+S2 | SC-001, SC-002 |
| S5 | SC-003 |
| S3 | SC-004 |
| S1 (köprü yok) + kod denetimi | SC-005 |