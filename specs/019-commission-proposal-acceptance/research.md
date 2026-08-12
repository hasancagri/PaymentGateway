# Research: Komisyon Teklifi ve Metin-Sürümlü Pazarlık

**Feature**: 019 | **Date**: 2026-08-11 | Tüm kararlar kullanıcıyla oturumda netleşti; açık
NEEDS CLARIFICATION kalmadı.

## R1 — Standart tarife türetme

- **Decision**: Ayrı tarife tablosu/ekranı YOK. Teklif anında `BankCommission` grid'inden türet:
  her `Criteria` için banka oranı + `CommissionProposalOption.DefaultMarginPoints` (sabit puan,
  config; Options POCO + `AddOptionsExt`, DataAnnotations zorunlu). Aynı kombinasyon birden çok
  bankada varsa her banka satırı ayrı üretilir (grid kombinasyon-bazlı, mevcut yapı).
- **Rationale**: Kullanıcı "ekranla uğraşmak istemiyorum" — sıfır ekran, sıfır buton. Fotoğraf
  (draft) teklif anında alındığından sonradan banka grid'i değişse bile teklif sabit kalır.
- **Alternatives considered**: Kalıcı StandardTariff tablosu + "Hesapla" (fazla parça, YAGNI);
  oransal marj (açıklaması zor); en-ucuz-banka bazlı (BankRouter zaten icra tarafında en ucuzu
  seçiyor, teklif yüzeyinde gereksiz akıl).

## R2 — Draft/Proposal ayrımı ve satır numarası

- **Decision**: İki aggregate: `CommissionDraft` (merchant başına TEK çalışma kopyası, mutable)
  ve `CommissionProposal` (gönderilmiş immutable fotoğraf + karar bileti). Draft satırları
  deterministik sıralanır (BankCode ASC, Installment ASC) ve 1-tabanlı satır numarası taşır;
  Excel'e aynı numara kolonu yazılır → "satır 37" adreslemesi birebir eşleşir. Revizyonlar
  draft'ı anında değiştirir; "gönder" draft'ın fotoğrafını yeni proposal yapar (eski Beklemede →
  Geçersiz), yeni bilet üretir.
- **Rationale**: Kullanıcının açık akışı: "satır 37'yi 1.85 yap → diff göster → merchant'a gönder".
  Düzenleme/gönderme fazlarının ayrıklığı (FR-010) kazara gönderimi imkânsız kılar.
- **Alternatives considered**: Tek aggregate içinde tur listesi (kabul-sonrası kilit ile mutable
  çalışma kopyası aynı nesnede karışıyor); satır no'suz yalnız banka+taksit adresleme (kullanıcı
  satır-no istedi; Excel bizim ürettiğimiz için sıra deterministik — güvenli).

## R3 — Revizyon komut modeli (LLM güvenliği)

- **Decision**: `revise_commission_draft` tool'u yapılandırılmış işlem listesi alır:
  `{op:"set", row:37, rate:1.85}` | `{op:"set", bank:"...", installment:6, rate:1.8}` |
  `{op:"set"|"delta", filter:{bank?/installment?}, rate?/delta?}`. Matematik (delta uygulama,
  taban karşılaştırma) SUNUCUDA. Taban ihlali → işlem BÜTÜN reddedilir, ihlal satırları listelenir.
  Yanıt = uygulanan diff listesi (satır, eski→yeni) → agent admin'e yankılar.
- **Rationale**: LLM yalnız çevirmen; oran üretmez, hesap yapmaz (kullanıcı şartı: insan
  inisiyatifi). Diff yankısı yanlış çeviriyi göze görünür kılar.
- **Alternatives considered**: Serbest metni sunucuda parse (LLM'in işini sunucuya taşır,
  kırılgan); LLM'in delta hesabını yapması (aritmetik halüsinasyon riski).

## R4 — Excel üretimi ve mail eki

- **Decision**: `SendEmailRequested`'e opsiyonel generic tablo eki eklenir:
  `EmailAttachmentTable(FileName, Headers, Rows)` (`Rows: string[][]`). Mail.Worker ClosedXML ile
  xlsx üretip `System.Net.Mail.Attachment` olarak ekler. Excel.Mcp bu akışta KULLANILMAZ
  (016: MCP yalnız agent yüzeyi); tablo→xlsx kod benzerliği bilinçli tekrar (üçüncü tüketici
  çıkarsa ortak çekirdek düşünülür).
- **Rationale**: Kullanıcı yönlendirmesi "Commission veriyi çeker, Mail tarafına gönderir";
  ClosedXML tek yerde (altyapı), BC Excel bilmez, mesaj domain değil generic tablo taşır.
- **Alternatives considered**: Commission.Api'de byte üretimi (ClosedXML BC'ye girer, tekrar
  riski); Excel.Mcp'ye HTTP yüzeyi açmak (016 kuralını deler); base64 hazır dosya mesajı
  (worker aptallaşır ama BC şişer).

## R5 — Karar uçları ve bilet

- **Decision**: Commission.Api'de anonim iki uç seti: `GET /commission-proposals/decision/
  {ticket}/accept|reject` (mini HTML onay/gerekçe formu) + `POST` karşılıkları. Bilet: tek
  kullanım + TTL (`CommissionProposalOption.TicketTtlHours`) + yalnız son teklif (proposal
  Geçersiz ise bilet ölü). Kabul POST'u: proposal Kabul → draft satırları `MerchantCommission`'a
  kopya → `MerchantCommissionGridReady` publish (aynı `[Transactional]` outbox) → mevcut
  `MerchantCommissionGridReadyHandler` zinciri (değişmez). Link taban adresi config
  (`PublicBaseUrl`) — mail içine mutlak URL yazılır.
- **Rationale**: FR-004 (yetki = bilet) aktivasyon redeem emsali; kabulün insansızlığı (SC-002)
  mevcut event zincirini aynen kullanarak sağlanır — Merchant BC'de sıfır değişiklik.
- **Alternatives considered**: ECommerce tarafına karar sayfası (iki repo, entegrasyon yükü);
  salt-JSON uçlar (ret gerekçesi formu yok, UX kötü); mail-yanıtı/LLM yorumu (kabul kararını
  LLM'e emanet etmek riskli — kullanıcı reddetti).

## R6 — Finalize'ın kaldırılması

- **Decision**: `FinalizeMerchantCommissionGrid` slice'ı + `/finalize` ucu + Admin UI Finalize
  butonu SİLİNİR. `MerchantCommissionGrid` aggregate'i ve `GridStatus` (Draft/Ready) kalkar —
  "hazır" olmanın tek yolu teklif kabulü. Admin komisyon ekranı salt-okuma kalır + teklif
  durumunu (proposal status) gösterir. Dev aşaması: geriye-uyum/migration YOK (DB sıfırlanabilir).
- **Rationale**: FR-013; iki paralel "hazır olma" yolu (Finalize + kabul) tutarsızlık üretir.
- **Alternatives considered**: Finalize'ı "teklifsiz hızlı yol" olarak tutmak (onaysız oran
  dayatması — feature'ın çözdüğü problemi geri getirir).

## R7 — Anayasa bayat atfı (bu feature dışı, kayıt)

- **Decision**: Anayasa II hâlâ "BaseModel'den türer" ve "Enumeration ile modellenir" diyor;
  ikisi de 2026-08-11 refactor'üyle silindi (PG #27 / EC #53). Ayrı PATCH amendment gerekir
  (`/speckit-constitution`); bu feature bloklanmaz.
- **Rationale**: Anayasa değişikliği kendi prosedürüyle yapılır; plan içinde gömülmez.

## R8 — Merchant.Agent genişlemesi

- **Decision**: Yeni proje YOK (kullanıcı tercihi: Admin.Agent yerine Merchant.Agent). Agent'a
  ikinci MCP client (Commission.Api `/mcp`) + 4 skill: `propose_commission`,
  `revise_commission_draft`, `show_commission_draft`, `commission_proposal_status`. İsim→merchant
  çözümü mevcut `get_merchant` (Merchant.Api /mcp) ile. Merchant.Agent client'ına
  `commission.write` scope'u eklenir (Identity seed güncellenir).
- **Rationale**: Merchant yaşam döngüsü agent'ı zaten bu (013 register/status); teklif de o
  döngünün adımı. LLM yalnız tool sırası kurar (007 ilkesi).
- **Alternatives considered**: Admin.Agent yeni proje (kullanıcı reddetti); Claude Code MCP
  konsolu (ürünleşmiş akış değil).