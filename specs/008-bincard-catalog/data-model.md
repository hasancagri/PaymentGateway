# Phase 1 — Data Model: BinCard Referans Kataloğu

Payment BC (Payment.Api). Marten document (referans/lookup) + saf çözümleme/mapping. Aggregate değil.

## Document: `BinCard`

Bir BIN numarasının kart/banka nitelikleri. Marten document; kimlik = `BinNumber`.

| Alan | Tip | Not |
|------|-----|-----|
| `BinNumber` | `string` | Kimlik (Marten Id). 6 hane. Exact-match erişim. |
| `BankCode` | `string` | 4 haneli banka kodu (yerel string referans; PosAccount.BankCode ile aynı uzay). |
| `CardType` | `CardType` (enum) | Debit / Credit |
| `CardBrand` | `CardBrand` (enum) | Unknown/Visa/MasterCard/Troy/Amex/Discover/Unionpay/JCB |
| `CardProgram` | `CardProgram` (enum) | Unknown/Axess/…/SaglamKart (indexli — türetme sorgusu) |
| `Commercial` | `bool` | Ticari kart mı |

**Marten kaydı** (Program.cs): `opts.Schema.For<BinCard>()` — Id = `BinNumber` (string identity);
`Index(x => x.CardProgram)` (taksit-banka türetme). `Store` upsert idempotency (kimlik BinNumber).

## Enum'lar (Payment domain, düz C#, CP.VPOS int değerleriyle birebir)

```
CardType   : Debit=0, Credit=1
CardBrand  : Unknown=-1, Visa=0, MasterCard=1, Troy=2, Amex=3, Discover=4, Unionpay=5, JCB=6
CardProgram: Unknown=-1, Axess=0, Bank24=1, Bankkart=2, Bonus=3, CardFinans=4, Maximum=5,
             MilesAndSmiles=6, Neo=7, Paraf=8, ShopAndFly=9, Wings=10, World=11, Advantage=12, SaglamKart=13
```

> Değerler CP.VPOS `CreditCardType/Brand/Program` ile aynı (parite). `BinCardMapping` sınırda çevirir;
> CP.VPOS tipi domain'e girmez.

## Çözümleme çıktısı: `CardInfo` (mevcut, değişmez)

```
record CardInfo(string? BankCode, bool IsCreditCard, IReadOnlyList<string> InstallmentBankCodes)
```

`ResolveBinCard` bunu döner (bulunamazsa **null**). `InstallmentBankCodes` çözüm anında türetilir.

## Import girdisi: `BinCardImportItem` (DTO)

Yayınlanan listenin tek kaydı. Alanlar `BinCard` ile aynı ama enum'lar taşınabilir biçimde
(int veya string kod); `BinCardMapping` domain enum'una çevirir. Geçersiz/eksik → atlanır+raporlanır.

## Kurallar / mantık (saf, test edilir)

- **BinCardMapping**: CP.VPOS `CreditCardBinQueryResponse`/int → `BinCard`. Bilinmeyen enum değeri →
  `Unknown` (marka/program) — çökmez.
- **8→6 fallback (ResolveBinCard)**: girdi > 6 hane → önce tam eşleşme, yoksa `binNumber[..6]`.
- **InstallmentBankCodes türetme**: `CardType == Credit && CardProgram != Unknown` ise katalogdan aynı
  `CardProgram` kayıtları → `BankCode` distinct (destek sayısına göre azalan sıralı); kartın `BankCode`'u
  listede yoksa eklenir ve **başa** alınır. Aksi halde boş liste. (CP.VPOS paritesi.)
- **Bulunamadı**: `ResolveBinCard` null döner; sahte `CardInfo` üretmez; istisna atmaz.
- **Import idempotency**: `Store(BinNumber kimlik)` — aynı liste iki kez → içerik/sayı değişmez.
- **Seed boş-kontrolü**: katalogda kayıt varsa seed atlanır.

## İlişkiler

- `BankCode` → PosAccount / banka kataloğu ile aynı kod uzayı (yerel referans, cross-BC değil).
- `CardInfo` → `BankRouter.Route` girdisi (mevcut routing/taksit tüketici).
- Seed kaynağı → CP.VPOS `VPOSClient.AllCreditCardBinList()` (salt-okunur, değişmez).