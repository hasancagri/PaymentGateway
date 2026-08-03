# Feature Specification: A2A Ödeme Oturumu — Kayıtlı Kartla Taksitli Ödeme (Model A)

**Feature Branch**: `007-a2a-payment-session`

**Created**: 2026-08-02

**Status**: Draft

**Input**: User description: "E-Ticaret tarafında kullanıcı 6 haneli kart bilgisi ile değil, kartın gateway tarafındaki karşılığı olan token ile gelecek. Kullanıcı metin üzerinden 'hesabımdaki şu kartı kullanarak sepetimdeki ürünleri satın almak istiyorum' der. Kullanıcı ilk başta taksit belirtemez; banka ve taksit bilgileri gateway'den gelmemiştir. A2A mantığı ile e-ticaret agent'ı niyeti Payment Gateway'e taşır. Fiyatlama Model A: komisyonu merchant yutar."

## Bağlam ve Kapsam

Bir e-ticaret sitesindeki asistan agent, kullanıcının doğal dille verdiği ödeme niyetini **A2A (agent-to-agent)** üzerinden Payment Gateway'e taşır. Kullanıcı kart bilgisi girmez; hesabındaki **kayıtlı kartın token'ı** ile gelir. Akış iki fazlıdır çünkü taksit seçenekleri karta bağlıdır: token çözülüp BIN/banka bilinmeden taksit gridi hesaplanamaz.

**Fiyatlama = Model A (merchant absorbs):** Kullanıcı her durumda sepet tutarını öder (peşin/taksit fark etmez, faizsiz görünür). Banka komisyonu (MSC) merchant'ın gideridir, settlement'ta merchant'tan kesilir. Komisyon **yalnızca** en ucuz POS'u seçmek için `BankRouter`'a girer; kullanıcının gördüğü tutara **yansımaz**.

**Güvenlik sınırı:** Kart verisi (tam PAN/CVV/expiry) **asla** LLM/A2A/metin kanalından geçmez. A2A kanalından yalnız *niyet* + *token* + *seçilen taksit* taşınır. Token'ın karta çözümü Payment Gateway içinde güvenli tarafta yapılır (PCI-DSS kapsamı gateway'de izole).

Bu spec **Payment Gateway (DropShop) tarafını** kapsar: A2A cephesi + oturum + iki fazlı akış. E-ticaret tarafındaki agent (ECommerceWithAgentFramework) bu yüzeyin *tüketicisidir* ve ayrı repoda ele alınır.

**Kapsam sınırı (007) — KARARLAŞTIRILDI:** Bu spec **taksit seçimine kadar**ki akışı kapsar: A2A yüzeyi + Payment agent (LLM) + MCP + Faz 1 taksit sorgu (quote) + taksit seçiminin oturuma yazılması + durum sorgu. **Fiili ödeme çekimi (pay) 007 dışıdır** — `ProcessPayment` hattı (VPOS satış, failover, 3D, `Payment` kaydı) **yeniden kurgulanacak ayrı bir feature**tir ve çok daha kapsamlıdır. 007, seçilen taksiti bir *seam* arkasında sonraki pay feature'ına devreder; kendi kodunda çekim yapmaz.

**Mimari (kararlaştırıldı):**

```
ECommerce agent (LLM)  ──A2A (task)──►  Payment agent (LLM)  ──MCP──►  Payment.Api MCP tool'ları  ──►  saf domain
   niyet + token                          MCP router               get_installment_options            BankRouter (quote)
                                          (ChatAgent deseni)        select_installment / status       token→BIN (vault seam)
                                                                    (process_payment ERTELENDİ)
```

- **Sınırda A2A** (ECommerce agent ↔ Payment agent): A2A agent'ları **opak** — tüketici, gateway'in iç MCP tool'larını görmez, yalnız bir *task* yollar. Org sınırını protokol korur.
- **İçeride MCP** (Payment agent → kendi servisinin tool'ları): ECommerce'deki ChatAgent deseninin aynısı. MCP org sınırını geçmez.
- **Payment agent LLM'i yönlendiricidir, karar verici değildir** — tool sırasını (quote → pay) kurar; para/banka/kart kararlarını **saf domain** verir. Tutar session'dan gelir ve sunucu-otoriter doğrulanır; banka `BankRouter` seçer; kart çözümü vault'ta. LLM bunları üretmez/uydurmaz.
- İki faz, A2A task yaşam döngüsüne oturur: quote → **`input-required`** (taksit seçimi beklenir) → seçim → **`completed`** (veya 3D için ara durum). `PaymentSession` = bu task'ın kalıcı domain izdüşümü.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Taksit seçeneklerini getir (quote) (Priority: P1)

Kullanıcı e-ticaret asistanına "hesabımdaki şu kartla sepetimi almak istiyorum" der. Asistan agent, kart token'ı ve sepet tutarını A2A üzerinden Payment Gateway'e iletir. Gateway token'ı çözer, kartın BIN'inden banka ve kart programını bulur, desteklenen **tüm** taksit seçeneklerini üretir ve agent'a döner. Her seçeneğin arkasında en ucuz destekleyen POS (`BankRouter`) vardır; kullanıcı bunu bilmez, sadece taksit listesini görür. Model A gereği her satırın tutarı sepet tutarıdır (şişmez).

**Why this priority**: Bu, akışın giriş kapısı ve tek başına değer üretir — kullanıcı kartıyla hangi taksitlerin mümkün olduğunu görür. Ödeme (Story 2) olmadan da test edilebilir ve gösterilebilir.

**Independent Test**: Bir token + sepet tutarı ile quote isteği gönderilir; dönen taksit listesi doğrulanır (yalnız desteklenen taksitler, her satır tutarı = sepet tutarı, geçersiz taksitler listede yok).

**Acceptance Scenarios**:

1. **Given** geçerli bir kayıtlı-kart token'ı ve sepet tutarı, **When** agent quote ister, **Then** kartın programının desteklediği tüm taksit seçenekleri döner; her satırda taksit sayısı, kullanıcı ödeyeceği toplam (= sepet tutarı) ve aylık tutar (sepet tutarı / taksit) bulunur.
2. **Given** kartın programının 6 taksiti destekleyen hiçbir aktif POS'u yok, **When** quote hesaplanır, **Then** 6 taksit seçeneği listede **görünmez** (kullanıcı seçemez).
3. **Given** kart bir banka kartı (kredi değil), **When** quote hesaplanır, **Then** yalnız "Peşin (tek çekim)" seçeneği döner; taksitli satırlar görünmez.
4. **Given** Model A, **When** herhangi bir taksit satırı üretilir, **Then** kullanıcıya gösterilen toplam tutar sepet tutarına eşittir — komisyon tutara **eklenmez**.

---

### User Story 2 - Taksit seçimini oturuma kaydet (select) (Priority: P1)

Kullanıcı dönen listeden bir taksit seçer (pazarlık yok — hazır listeden seçim). Asistan agent, aynı oturumda token + seçilen taksit sayısını Payment Gateway'e iletir. Gateway seçimin sunulan listede olduğunu doğrular ve oturuma yazar (faz: taksit seçildi). **Fiili çekim burada YAPILMAZ** — seçim, sonraki pay feature'ının tüketeceği bir *seam*'e bırakılır; oturum "taksit seçildi" fazında kalır.

**Why this priority**: Faz 1 (quote) tek başına listeyi gösterir; seçimin oturuma yazılması iki fazlı A2A akışını (`input-required` → seçim) tamamlar ve pay feature'ının bağlanacağı devir noktasını üretir. P1.

**Independent Test**: Story 1'den dönen bir taksit seçimiyle select isteği gönderilir; oturumun "taksit seçildi" fazına geçtiği ve seçilen taksitin kaydedildiği doğrulanır. Sunulmayan bir taksit reddedilir.

**Acceptance Scenarios**:

1. **Given** açık bir ödeme oturumu (quote verildi) ve sunulan listede olan bir taksit seçimi, **When** agent select ister, **Then** seçim oturuma yazılır ve oturum "taksit seçildi" fazına geçer.
2. **Given** kullanıcı, quote'ta **olmayan** bir taksit sayısıyla select ister, **When** doğrulama yapılır, **Then** reddedilir (yalnız sunulan seçeneklerden seçilebilir).
3. **Given** zaten "taksit seçildi" fazındaki bir oturum, **When** ikinci bir select gelir, **Then** seçim güncellenir veya reddedilir (idempotent/tutarlı davranış; çekim tetiklenmez).

> **Ertelendi (ayrı feature):** Fiili ödeme çekimi — `BankRouter` maliyet sıralı adaylar, VPOS satış, failover, 3D yönlendirme, `Payment` kaydı ve çekilen tutar doğrulaması — 007 **dışıdır**. `ProcessPayment` hattı yeniden kurgulanacak; 007 yalnız seçilen taksiti seam'e devreder.

---

### User Story 3 - Ödeme oturumu durumunu sorgula (Priority: P2)

Agent (veya arka planda 3D dönüşü sonrası) ödeme oturumunun güncel durumunu sorgular: quote verildi / taksit seçildi / ödeme bekliyor / 3D bekliyor / tamamlandı / başarısız. Böylece asistan kullanıcıya doğru durumu bildirebilir ve 3D yönlendirmesi sonrası sonucu öğrenebilir.

**Why this priority**: Akışı bağlar (özellikle 3D dönüşü asenkron olduğu için) ama çekirdek quote+pay olmadan tek başına anlamsız. P2.

**Independent Test**: Bir oturum açıp durumunu sorgula; her faz geçişinden sonra dönen durumun doğru olduğu doğrulanır.

**Acceptance Scenarios**:

1. **Given** açılmış bir ödeme oturumu, **When** durum sorgulanır, **Then** oturumun bulunduğu faz (quote verildi / ödeme tamamlandı / 3D bekliyor / başarısız) döner.
2. **Given** 3D yönlendirmesi tamamlanmış, **When** durum sorgulanır, **Then** nihai sonuç (tamamlandı/başarısız) döner.

---

### Edge Cases

- **Token geçersiz/süresi dolmuş/başka kullanıcıya ait**: quote/pay reddedilir; kart verisi sızmadan anlamlı hata döner.
- **Bilinmeyen BIN** (yerli BIN tablosunda yok, muhtemelen yabancı kart): taksit üretilemez veya yalnız peşin; ödeme tarafında güvenli varsayım (3D zorunlu — mevcut router kuralıyla uyumlu).
- **Hiç aktif POS yok / kartın programını destekleyen POS yok**: quote boş taksit listesi yerine anlamlı "ödeme alınamıyor" durumu döner (peşin dahi mümkün değilse).
- **Kullanıcı, quote'ta olmayan bir taksit sayısıyla select ister**: reddedilir (yalnız sunulan seçeneklerden seçilebilir).
- **Aynı oturumda tekrar select**: idempotent/öngörülebilir (çift faz geçişi yok). *(Tamamlanmış-ödeme idempotency'si pay feature'ında.)*
- **Quote ile çekim arasında POS/komisyon değişti**: *(pay feature'ına ertelendi — çekim taze hesaplar.)*
- **Sepet tutarı sıfır/negatif**: reddedilir.
- **A2A kanalına yanlışlıkla kart verisi konması**: yüzey yalnız token kabul eder; tam kart alanları A2A sözleşmesinde **yoktur**.

## Requirements *(mandatory)*

### Functional Requirements

**A2A cephesi, Payment agent ve oturum**

- **FR-001**: Sistem, bir e-ticaret agent'ının A2A üzerinden ödeme niyeti iletebileceği bir **Payment agent** yüzeyi sunmalı (Agent Card ile yetenek ilanı); bu yüzey yalnız *niyet + token + sepet tutarı + (fazına göre) seçilen taksit* kabul etmeli, tam kart verisi **kabul etmemeli**.
- **FR-002**: Payment agent, gelen task'ı kendi servisinin **MCP tool'larına yönlendirmeli** (`get_installment_options`, `select_installment`, `payment_status`); bu tool'lar saf domain'i (Model A quote + `PaymentSession`) sarmalı. *(`process_payment` tool'u pay feature'ında eklenecek.)*
- **FR-003**: Payment agent LLM'i yalnız **tool sırasını** (quote → select) kurmalı; tutar, banka ve kart kararlarını **üretmemeli** — bunlar domain/vault tarafından belirlenir. Tutar session'daki değerden alınır ve sunucu-otoriter doğrulanır (LLM'in ürettiği tutar kabul edilmez).
- **FR-004**: Sistem, bir ödeme akışını **oturum (PaymentSession)** olarak izlemeli: açılış → taksit sunumu → taksit seçimi → ödeme sonucu fazlarını tek kimlik altında takip etmeli; A2A task yaşam döngüsüne (`input-required` → `completed`) izdüşmeli.
- **FR-005**: Kullanıcı/agent, oturumun güncel durumunu sorgulayabilmeli (faz + nihai sonuç).

**Faz 1 — taksit sorgu (quote)**

- **FR-006**: Sistem, verilen token'ı güvenli tarafta karta çözmeli ve kartın BIN'inden banka + kart programı bilgisini elde etmeli. Token çözümü A2A/LLM kanalı dışında kalmalı.
- **FR-007**: Sistem, kartın programının **desteklediği tüm taksit sayıları** için seçenek üretmeli; her seçenek için o taksiti destekleyen en düşük maliyetli POS'u (`BankRouter`) seçmeli.
- **FR-008**: Sistem, belirli bir taksit sayısını destekleyen aktif POS yoksa o seçeneği listeye **koymamalı** (kullanıcı seçemez).
- **FR-009**: Sistem, kart kredi kartı değilse taksitli seçenekleri üretmemeli; yalnız peşin (tek çekim) sunmalı.
- **FR-010**: **Model A** — Sistem, her taksit seçeneğinde kullanıcıya gösterilen toplam tutarı **sepet tutarına eşit** üretmeli; banka komisyonu (MSC) kullanıcı tutarına **eklenmemeli**. Aylık tutar = sepet tutarı / taksit sayısı.
- **FR-011**: Banka komisyonu yalnız **POS seçiminde** (en ucuz aday) kullanılmalı; sonuç kullanıcı fiyatını etkilememeli. (Not: mevcut `GetInstallmentOptions` bugün komisyonu tutara ekliyor — Model A için bu düzeltilmeli.)

**Faz 2 — taksit seçimi (select)**

- **FR-012**: Kullanıcı yalnız Faz 1'de sunulan taksit seçeneklerinden birini seçebilmeli; listede olmayan taksit reddedilmeli.
- **FR-013**: Sistem, seçilen taksiti oturuma yazmalı ve oturumu "taksit seçildi" fazına geçirmeli; seçim, sonraki pay feature'ının tüketeceği bir *seam*'e devredilmeli. **007 fiili çekim yapmaz.**

**Ertelendi — ayrı pay feature (007 dışı):** Aşağıdakiler `ProcessPayment` yeniden kurgusuyla gelecek; 007 kapsamında **değildir**:

- ~~FR-014 (deferred)~~: seçilen taksit için satış hattı (`BankRouter` adayları + VPOS + failover + 3D).
- ~~FR-015 (deferred)~~: **Model A** çekim tutarı = sepet tutarı (bankaya vade farkı bindirilmez). *(Not: Model A'nın quote tarafı FR-010'da 007 içinde; çekim tarafı ertelendi.)*
- ~~FR-016 (deferred)~~: ödeme sonucu (işlem kimliği / hata / 3D içeriği / denenen banka kodları) oturuma yazımı.

**Tutarlılık ve güvenlik**

- **FR-017**: Taksit seçimi, oturumun bir taksit *sunulmuş* fazında olmasını gerektirmeli; quote yapılmamış oturuma select reddedilmeli. *(Quote↔çekim tutar tutarlılığı pay feature'ına ertelendi.)*
- **FR-018**: Oturum faz makinesi tutarlı olmalı; tekrarlı select idempotent/öngörülebilir davranmalı (çift faz geçişi yok). *(Tamamlanmış-ödeme idempotency'si pay feature'ında.)*
- **FR-019**: Token geçersiz/süresi dolmuş/yetkisizse quote ve select reddedilmeli; hata mesajı kart verisi sızdırmamalı.
- **FR-020**: Sistem yalnız TL işlem desteklemeli (yabancı para dışı — mevcut proje kuralı).

### Key Entities *(include if feature involves data)*

- **Ödeme Oturumu (PaymentSession)**: Bir agent-başlatımlı akışın kimliği ve durumu. Nitelikler: token referansı, sepet tutarı, sunulan taksit seçenekleri, seçilen taksit, faz/durum (**oturum açıldı / taksit sunuldu (quote verildi) / taksit seçildi / başarısız**). *(3D bekliyor / tamamlandı fazları ve `Payment` kaydı bağı pay feature'ına ertelendi.)*
- **Kart Token'ı**: Kullanıcının kayıtlı kartının, gerçek kart verisi yerine geçen referansı. Gateway üretir, güvenli tarafta BIN'e çözülür (007'de yalnız BIN/kart-programı gerekir; tam PAN çekim feature'ında). (Tokenizasyon/vault bu projede **ayrı feature** — Dependencies'e bakınız.)
- **Taksit Seçeneği**: Bir taksit sayısı için kullanıcıya sunulan satır: taksit sayısı, kullanıcı toplam tutarı (= sepet tutarı, Model A), aylık tutar. (Arka planda seçilen POS/banka kullanıcıya gösterilmez.)
- **Payment (mevcut, 007 dışı)**: Fiili satış kaydı; yeniden kurgulanacak `ProcessPayment` pay feature'ı üretecek. 007 bu kayda **yazmaz**; yalnız seçilen taksiti seam'e devreder.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kullanıcı, tek bir doğal-dil talebiyle ("şu kartla sepetimi al") taksit seçeneklerini görene kadar elle kart bilgisi girmez — akışta girilen kart alanı sayısı **sıfırdır**.
- **SC-002**: Taksit seçenekleri listesindeki her satırın kullanıcı tutarı sepet tutarına **eşittir** (Model A doğrulaması; sapma toleransı 0).
- **SC-003**: Kullanıcıya yalnız gerçekten ödeme alınabilecek taksitler sunulur — desteklenmeyen taksit sayısı listede **hiç görünmez** (%100 filtre doğruluğu).
- **SC-004**: Taksit seçimine kadar kullanıcı, taksit sayısı dışında hiçbir teknik seçim (banka, POS) yapmaz.
- **SC-005** *(pay feature'ına ertelendi)*: Quote ile çekim arasında konfigürasyon sabitken, gösterilen tutar ile çekilen tutar arasında fark yoktur. *(007 çekim yapmadığından bu kriter sonraki feature'da doğrulanır.)*
- **SC-006**: Kart verisi (tam PAN/CVV) A2A/metin kanalında **hiç** taşınmaz — kanal sözleşmesinde bu alanlar bulunmaz.

## Assumptions

- **Fiyatlama Model A** seçildi (merchant komisyonu yutar). Model B (vade farkı / kullanıcıya yansıtma) bilinçli olarak kapsam dışı ve ertelendi (bkz. Obsidian `Yapılacaklar` notu). Bu, taksit tutarlarının sepet tutarına eşit olmasını gerektirir.
- **Quote için** mevcut `BankRouter`, `PosAccount` ve BIN çözümü **yeniden kullanılır**; BIN → `CardInfo` çözümü **008 (BinCard→DB) `ResolveBinCard`**'tan gelir (`CP.VPOS` BinService değil — 008 native çözüm merge edildi). Bu spec bunların üstüne A2A + oturum + Model A quote'u ekler. **`ProcessPayment` (VPOS satış + failover + 3D) 007'de kullanılmaz** — yeniden kurgulanacak ayrı pay feature'ıdır; 007 seçilen taksiti bir seam'e devreder.
- `GetInstallmentOptions` bugün komisyonu toplam tutara ekliyor (Model B davranışı); Model A için kullanıcı tutarı = sepet tutarı olacak şekilde düzeltilecektir.
- Taksit sayısı kümesi, POS'ların komisyon gridinden türetilir (sabit `[1,2,3,6...]` listesi varsayılmaz) — hayali/ desteklenmeyen satır üretilmez.
- E-ticaret tarafındaki asistan agent bu repoda değildir; Payment Gateway herhangi bir agent'ın çağırabileceği bir A2A yüzeyi sunar. Agent tarafı entegrasyonu ayrı repoda (ECommerceWithAgentFramework) ele alınır.
- Yetkilendirme henüz yok (proje geneli erteleme, Identity BC ile gelecek); A2A yüzeyi şimdilik korumasız, güvenlik sınırı yalnız "kart verisi kanala girmez" ilkesiyle sağlanır.
- Para birimi yalnız TL.
- Test: proje kuralı gereği saf domain birim testleri yazılır; banka HTTP çağrıları / A2A entegrasyonu birim testi edilmez, quickstart senaryolarıyla elle doğrulanır.

## Dependencies

- **Kart tokenizasyonu / kart kasası (vault) — bu projede AYRI feature**: Token → kart çözümü, Payment Gateway içinde inşa edilecek ayrı bir tokenizasyon mekanizmasınca sağlanır (kullanıcı kararı: "bu projede kart bilgilerini tokenize eden bir mekanizma olacak"). Bu spec o mekanizmayı **inşa etmez**; yalnız "geçerli token → kart (BIN/PAN) çözümü" yeteneğini **tüketir**. Kendi spec'iyle (muhtemelen 008) gelir. Sözleşme: gateway token üretir/çözer; e-ticaret tarafı yalnız token'ı saklar.
  - Bağlam (dış repo): ECommerceWithAgentFramework'te `Customer.Wallet` + `SavedCard` + `ICardTokenizer` zaten var; bugün `SimulatedCardTokenizer` kullanıyor. Plan: PaymentGateway gelince tokenizer bu gateway'in ucunu çağıracak. Yani token **gateway-üretimi**, e-ticaret saklayıcı.
- **A2A taşıma katmanı**: `.NET` + **a2a-dotnet** (Yapılacaklar'da seçili). Payment agent Agent Card (`/.well-known/agent-card.json`) ile yeteneklerini ilan eder: `quote-installments`, `select-installment`, `payment-status`. *(`pay-with-token` yeteneği pay feature'ında eklenecek.)*
- **Agent framework**: Payment agent, ECommerce ChatAgent deseniyle **Microsoft Agent Framework + MCP** kullanır; kendi servisinin MCP tool'larını çağırır.