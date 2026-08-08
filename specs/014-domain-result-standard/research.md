# Phase 0 Research: Domain Sonuç Sarmalama Standardı

Tüm NEEDS CLARIFICATION geliştiriciyle çözüldü. Kararlar:

## Karar 1 — Kapsam call-site tabanlı

- **Decision**: Standart yalnız **handler'dan (Command/Query slice handler) çağrılan** aggregate
  davranış/fabrika metotlarını kapsar. Belirleyici kriter çağrı yeri (handler), metot sınıfı değil.
- **Rationale**: Sonuç zarfının değeri handler'ın sonucu HTTP/mesaj sözleşmesine çevirdiği sınırda
  ortaya çıkar. Handler'dan erişilmeyen iç metotları sarmak değer üretmeden gürültü ekler.
- **Alternatives**: (a) Tüm public aggregate metotları — fazla geniş, iç yardımcıları da sarar.
  (b) Yalnız "davranış" sezgisel sınıflaması — call-site kadar kesin/denetlenebilir değil.

## Karar 2 — Outcome-enum sarımı

- **Decision**: Çok-durumlu domain sonucu dönen metotlar (ör. `ChallengeOutcome`)
  `ResultDomain<TEnum>.Ok(outcome)` ile sarılır; enum'un "başarısız" durumları `Error`'a eşlenmez.
- **Rationale**: `Failed`/`Expired` gibi durumlar iş akışında meşru sonuç (ör. Failed = tekrar
  denenebilir), teknik hata değil. Yorumu çağıran verir; zarf yalnız tek-tiplik sağlar.
- **Alternatives**: Passed=Ok / diğer=Error eşlemesi — semantik kaybı (retry-able Failed'i hata
  gibi gösterir) ve çağıranda bilgi kaybı.

## Karar 3 — Getter muafiyeti

- **Decision**: Saf sorgu/getter (property, `bool Is...`, `Get...` lookup, hesap) handler'dan
  çağrılsa bile muaf; ham değer döner. Örn. `PosAccount.GetCommissionRate(int)`.
- **Rationale**: Durum değiştirmeyen okuma "sonuç" değildir; zarf başarı/başarısızlık taşımaz,
  yalnız boilerplate ekler.
- **Alternatives**: Her şeyi sar — çağıran tarafta gereksiz `.Data` açımı, okunabilirlik kaybı.

## Karar 4 — Fabrika muafiyeti YOK

- **Decision**: Handler'dan çağrılan `Issue`/`Create` fabrikaları, asla başarısız olmasalar dahi
  `ResultDomain<T>.Ok(...)` ile sarılır (mesaj listesi boş kalır).
- **Rationale**: Tek-tiplik — çağıran her fabrika sonucunu aynı desenle işler; ileride doğrulama
  eklenirse imza değişmez (kırılma önlenir).
- **Alternatives**: Yalnız doğrulama yapan fabrikayı sar — imza tutarsızlığı, ileride kırılgan.

## Karar 5 — ECommerce ResultDomain varlığı

- **Open (envanter ajanı çözecek)**: ECommerce'de `ResultDomain` tipi var mı, nerede? Yoksa Common'a
  eklenmesi ECommerce plan'ının ilk task'ı olur. PaymentGateway'de mevcut
  (`src/others/Common/Results/ResultDomain.cs`).

## Uygulama deseni (referans)

Ham → sarılı dönüşüm çağıran güncellemesiyle atomiktir:

```
// önce
var outcome = challenge.Verify(value, now);        // ChallengeOutcome
// sonra
var result = challenge.Verify(value, now);         // ResultDomain<ChallengeOutcome>
if (!result.IsSuccess) return FeatureObjectResultModel<...>.Error(result.Messages);
var outcome = result.Data!;
```

`ResultDomain` API (Common): `Ok()`, `Ok(data)`, `Error(List<MessageItem>)`, `Error(MessageItem)`;
alanlar `IsSuccess`, `Messages`, `Data?`.
