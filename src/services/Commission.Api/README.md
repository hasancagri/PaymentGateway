# Commission.Api

Banka + merchant komisyonu. Tek BC, **iki aggregate** (Marten + Wolverine).

- **`BankCommission`** — gateway maliyeti (banka × `Criteria` → oran). Global. Benzersizlik
  `(BankCode, Criteria)`.
- **`MerchantCommission`** — gateway geliri (merchant × `BankCommission` → oran). Invariant
  `Rate > bankCommission.Rate` (**kesin büyük**, eşit reddedilir; kod `MERCHANT_RATE_MUST_EXCEED_BANK_RATE`),
  aggregate metodunda, **in-process** (aynı serviste iki aggregate → dağıtık invariant yok).
- **`Criteria`** (SharedKernel) — kart markası × tip × bölge × **taksit**; invariant taksit-taksit.
- **Merchant ↔ Commission cross-call YOK**: `MerchantId` yalnız `Guid` (imzalı claim'e güven ilkesi).
- **Uçlar:** `bank-commissions` (POST/GET), `merchant-commissions` (POST/PUT/GET?merchantId=).
  Bu dilimde **korumasız**.

> `Domains/SharedKernel/` = bu BC içinde iki aggregate'in paylaştığı çekirdek (Criteria + kart
> enum'ları). `src/others/Shared` PROJESİ ile karıştırma: o proje servisler-arası integration
> event kontratları içindir; domain tipleri BC izolasyonu gereği oraya konmaz.

Spec: `specs/001-merchant-onboarding-key/`.

## Bu dilimde YOK (ertelendi → Obsidian `DropShop/Yapılacaklar.md`)

- **Marten conjoined multitenancy** → şimdilik düz `Where(MerchantId == ...)` filtresi (SC-004).
- **BankCommission ↔ PosAccount (Payment.Api) çift kaynak uzlaştırma** → sonraki dilim.
- **Yetki/scope enforcement** (`commissions.manage`) → Identity dilimi.
- **Kart enum'ları düz enum** (kullanıcı direktifi "şimdilik"); ileride Enumeration smart-enum adayı.