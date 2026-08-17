# Data Model: Ödeme Süreci A2A + MCP Üzerinden (038)

**Date**: 2026-08-16 · **Spec**: [spec.md](spec.md) · **Research**: [research.md](research.md)

## Mevcut (değişmez) varlıklar

### Payment (aggregate — Payment BC, mevcut)

Çekim sonucu kaydı (033). Bu iş davranış EKLEMEZ; `ChargeSavedCardForAgent` slice'ı mevcut
fabrika/davranışı kullanır.

| Alan | Not |
|------|-----|
| Id, MerchantId | Kiracı sınırı |
| ProviderPaymentId | iyzico ödeme numarası |
| Price, PaidPrice, Installment | TL; taksitli toplam |
| Status | Succeeded / Failed (mevcut model) |

### StoredCard (aggregate — Payment BC, mevcut, DOKUNULMAZ)

Merchant-scoped saklı kart (032 Model A). **Müşteri alanı YOK** (yalnız `MerchantId`) —
bu yüzden gateway'de kart listeleme/seçim yüzeyi açılmaz (kullanıcı kararı); kart yalnız
`Token` (vault token) ile çözülür. **Kart ekleme/silme (tokenize/revoke) agent yüzeyine
AÇILMAZ** — güvenlik kararı: PAN agent/LLM bağlamına girmez; ekleme yalnız mevcut ekran →
HTTP yolundan.

| Alan | Not |
|------|-----|
| Token | Opak dış referans — A2A/MCP'de kartın KİMLİĞİ (R2) |
| CardUserKey, CardToken | iyzico sırları — DIŞARI SIZMAZ |
| Bin, Last4, Brand, Expiry, HolderName | Görüntü alanları (bu işte kullanılmaz; EC cüzdanı kendi kopyasını gösterir) |
| MerchantId | Kiracı sınırı; token çözümünde merchant eşleşmesi doğrulanır |
| Status | Active=0 / Revoked=1; Revoked kartla işlem RET |

## Yeni varlık

### MerchantStatusReference (doküman — Payment BC, YENİ; aggregate DEĞİL)

`merchant.lifecycle` fanout'undan beslenen event-fed read model (R4). Davranış taşımaz;
010 Reference deseni.

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | = MerchantId (upsert anahtarı) |
| Status | string | Event'teki status string'i aynen ("Active"/"Passive"/"Suspended") — BC enum'u sızmaz (012 kuralı) |
| UpdatedAtUtc | DateTime | Son event zamanı |

**Doğrulama/geçiş kuralları**: Yazan tek yer `MerchantLifecycleEventHandler` (idempotent
upsert; `MerchantCreated` → event'teki statü, `MerchantStatusChanged` → yeni statü).
Okuyan tek yer `ChargeSavedCardForAgent`: kayıt YOK veya Status ≠ "Active" → fail-closed
RET (sağlayıcıya gidilmez).

## Slice-içi sözleşme tipleri (kalıcı değil)

### Taksit Seçeneği (mevcut biçim — `InstallmentOptionItem`)

| Alan | Not |
|------|-----|
| InstallmentNumber | 1 = tek çekim |
| TotalPrice | O taksidin toplam ödenecek tutarı |

### A2A Ödeme İsteği (ChatAgent → Payment.Agent; contracts/a2a-payment-agent.md)

Yapılandırılmış payload — kalıcı değil, A2A mesajı. Kart çözümü ECommerce'de bittiği için
istek HAZIR vault token'la gelir; kart listeleme/ekleme niyeti A2A'ya HİÇ gelmez. Sepet kalemi de gelmez — gateway sentezler (R3).

| Alan | Kaynak | Not |
|------|--------|-----|
| intent | ChatAgent yorumu | `installments` \| `charge` (yalnız iki niyet) |
| merchantId | EC config (DropShopGateway) | Statü kapısı bunun üstünden (R4) |
| vaultToken | get_payment_context (varsayılan ya da cardId ile seçilen kart, R2/R7) | Her istekte zorunlu |
| amount | Sepet toplamı (get_basket) | Her istekte zorunlu |
| installment, paidPrice | Kullanıcı seçimi + taksit sorgusu sonucu | Yalnız `charge` |
| buyer (ad, soyad, e-posta, GSM, TCKN, adres, şehir, ülke, IP) | get_payment_context — GERÇEK müşteri (profil + varsayılan adres; TCKN/ülke/IP sabit — R3) | Yalnız `charge`; verbatim taşınır, LLM üretmez/göstermez |
| basketItems | — TAŞINMAZ | Gateway tek sentetik kalem sentezler (IyzicoRequestOptions, R3) |

**Kural**: PAN/CVC/cardUserKey/cardToken bu yapının HİÇBİR alanında yer alamaz (SC-003).

## İlişkiler

```
merchant.lifecycle (RabbitMQ fanout, Shared kontrat — 012)
   └─> MerchantLifecycleEventHandler ──upsert──> MerchantStatusReference (Payment DB)
                                                        │ (statü kapısı, fail-closed)
A2A isteği (vaultToken hazır) ──> Payment.Agent ──MCP──> ForAgent slice ─┤
                                                        ├─ StoredCard (token çözümü, Active kontrol)
                                                        └─ Payment (çekim kaydı)
EC tarafı: get_basket + list_cards/get_payment_context (cüzdan+adres defteri) ──> ChatAgent ──A2A──> yukarısı
Kart ekleme: EC ekran formu ──HTTP──> gateway tokenize ucu (agent yüzeyi DIŞI, değişmez)
```