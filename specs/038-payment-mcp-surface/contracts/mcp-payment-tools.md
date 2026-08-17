# Contract: Payment.Api /mcp Tool'ları (038)

**Yüzey**: `Payment.Api` `/mcp` (MCP over HTTP). **Tek tüketici**: `Payment.Agent`
(makine token'ı, policy `payment.write` — 011 deseni). ChatAgent veya BC kodu BAĞLANMAZ.

Her tool aggregate kökündeki `PaymentMcpTools` static class'ında durur ve YALNIZ kendi
`<X>ForAgent` slice'ını `IMessageBus.InvokeAsync` ile çağırır (015/016).

## Tool 1: `get_installment_options`

Kayıtlı kart + tutar için taksit seçenekleri. READ-ONLY; çekim yapmaz.

**Input**:

| Alan | Tip | Zorunlu | Not |
|------|-----|---------|-----|
| merchantId | Guid | ✓ | Kiracı sınırı |
| vaultToken | string | ✓ | StoredCard opak referansı |
| amount | decimal | ✓ | Sepet toplamı (TL) |

**Output** (`FeatureObjectResultModel<InstallmentOptionsView>`):

| Alan | Not |
|------|-----|
| options[] | `{ installmentNumber, totalPrice }` — 1 = tek çekim |
| bin, cardAssociation (opsiyonel görüntü) | Maskeli/hassas olmayan; PAN/CVC/iyzico sırrı YOK |

**Hatalar** (`MessageItem`, Result pattern — exception yok): kart bulunamadı / Revoked;
merchant uyuşmazlığı; sağlayıcı hatası ("seçenek alınamadı" — teknik ayrıntı sızmaz).

## Tool 2: `charge_saved_card`

Kayıtlı karttan GERÇEK çekim. Önkoşul: kullanıcı onayı ChatAgent'ta alınmış (yönerge kuralı).

**Input**:

| Alan | Tip | Zorunlu | Not |
|------|-----|---------|-----|
| merchantId | Guid | ✓ | Statü kapısı bu id üstünden (fail-closed) |
| vaultToken | string | ✓ | StoredCard opak referansı |
| amount | decimal | ✓ | Sepet toplamı (price) |
| paidPrice | decimal | ✓ | Seçilen taksidin toplam tutarı (taksit sorgusundan) |
| installment | int | ✓ | 1 = tek çekim |
| buyerName..buyerIp (9 düz alan) | string | ✓ | GERÇEK müşteri buyer'ı (EC get_payment_context'ten verbatim: ad, soyad, e-posta, GSM, TCKN, adres, şehir, ülke, IP) |
| basketItems | — | — | GÖNDERİLMEZ — gateway tek sentetik kalemle sentezler (IyzicoRequestOptions: BasketItemId/Name/Category; price = amount) |

**Output** (`FeatureObjectResultModel<ChargeResultView>`):

| Alan | Not |
|------|-----|
| paymentId | Gateway ödeme kaydı (Guid) |
| providerPaymentId | iyzico ödeme numarası |
| status | Succeeded / Failed |
| price, paidPrice, installment | Teyit alanları |

**Davranış sırası (slice içinde)**:
1. `MerchantStatusReference` oku → yok/≠Active → RET (sağlayıcıya gidilmez)
2. `StoredCard`'ı vaultToken+merchantId ile çöz → yok/Revoked → RET
3. Buyer VO doğrulaması (gerçek müşteri verisi) + tek sentetik sepet kalemi (config)
4. iyzico çekim (mevcut 033 wire deseni, slice-nested; literal'ler `IyzicoRequestOptions`)
5. Başarı → `Payment` kaydı + mevcut event akışı; başarısızlık → Failed kaydı (033 aynı)

**Hatalar**: statü kapısı reddi; kart bulunamadı/Revoked; sağlayıcı reddi ("ödeme alınamadı").

## Bilinçli YOK'lar

- Kart listeleme/ekleme/silme tool'u YOK — kart yönetimi ECommerce cüzdanı + ekran yolu
  (güvenlik kararı; StoredCard'da müşteri alanı da yok).
- `quote_installments_by_bin` benzeri BIN tool'u bu yüzeye EKLENMEZ — BIN quote akışı (024)
  Payment.Agent skill'i olarak zaten var; ihtiyaç doğarsa ayrı iş.