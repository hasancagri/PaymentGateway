# Phase 0 Research: Bank Referansı + Komisyon Grid

Bu dilimde açık teknik bilinmeyen ("NEEDS CLARIFICATION") yoktur; teknoloji ve pattern'ler mevcut
kod tabanından ve anayasadan sabittir. Aşağıda alınan kararlar ve gerekçeleri.

## Karar 1: Bank kimliği — Marten `Id` (Guid) + iş anahtarı `Code`

- **Karar**: Aggregate `AggregateRoot`'tan türer (Marten belge kimliği `Guid Id`). Banka kodu `Code`
  (4 hane) iş anahtarıdır; rota ve benzersizlik `Code` sorgusuyla sağlanır.
- **Gerekçe**: Mevcut tüm aggregate'ler (`BankCommission`, `MerchantCommission`) `AggregateRoot`/Guid
  kimliği kullanıyor; tutarlılık için aynısı. `Code` string'i Marten kimliği yapmak mevcut `BaseModel`
  düzeninden sapma olurdu.
- **Alternatif (red)**: `Code`'u Marten string kimliği yapmak — `BaseModel` Guid `Id` sabitini bozar,
  audit alanlarıyla çakışır.

## Karar 2: Benzersizlik (Code) handler'da zorlanır

- **Karar**: `CreateBank` handler'ı, aynı `Code` + `!IsDeleted` var mı sorgular; varsa `RECORD_DUPLICATE`.
- **Gerekçe**: Mevcut `CreateBankCommission` benzersizliği aynı şekilde handler'da zorluyor (Marten'de
  bileşik unique index yerine handler kontrolü konvansiyonu). Tutarlı.
- **Alternatif (red)**: Marten unique index — mevcut konvansiyona aykırı, soft-delete ile birlikte
  davranışı karışık.

## Karar 3: Desteklenen taksitler — aggregate içinde `List<int>`, doğrulanır

- **Karar**: `SupportedInstallments` private `List<int>`; `Create`/`Update` içinde tekilleştir + sırala,
  boş değil, her biri 1..`MaxInstallment(15)`.
- **Gerekçe**: İlke II (zengin domain, private koleksiyon, aggregate içi invariant). Üst sınır 15,
  CP.VPOS `SaleInfo [Range(1,15)]` ile tutarlı.
- **Alternatif (red)**: Global sabit taksit seti — kullanıcı bankaya-özgü taksit istedi; her banka farklı.

## Karar 4: Silme — soft-delete + bağlı-komisyon guard

- **Karar**: `DeleteBank` handler'ı bankaya bağlı (`BankCode` eşleşen, `!IsDeleted`) `BankCommission`
  var mı bakar; varsa yeni kod `BANK_HAS_COMMISSIONS` ile reddeder; yoksa `Bank.SoftDelete()`.
- **Gerekçe**: `BaseModel.IsDeleted` mevcut soft-delete deseni; yetim komisyon veri bütünlüğünü bozar
  (FR-007, SC-005). Hard-delete tehlikeli.
- **Alternatif (red)**: Cascade silme — komisyon verisi sessizce kaybolur, izlenemez.

## Karar 5: Toplu komisyon girişi — `BankCommission` slice'ına yeni endpoint

- **Karar**: `POST /bank-commissions/bulk` (BankCode + Item listesi). Her item için
  `(BankCode, Criteria)` var → `UpdateRate`, yok → `Create`. `[Transactional]` (tek atomik işlem).
- **Gerekçe**: Grid kaydını tek istekte atomik yapar; mevcut tek-tek `POST /bank-commissions` bozulmadan
  kalır (geriye uyum). Upsert mantığı `CreateMerchantCommission`'daki mevcut "var→update, yok→create"
  desenini izler.
- **Alternatif (red)**: Grid'in her hücreyi ayrı POST'lamas — N istek, kısmi başarı riski, atomik değil.

## Karar 6: Seed YOK

- **Karar**: Banka seed edilmez; operatör elle girer. Marten `IInitialData` kullanılmaz.
- **Gerekçe**: Kullanıcı açık talebi. Ayrıca CP.VPOS listesini seed'e kopyalamak sınır/gürültü ekler;
  elle giriş daha basit ve YAGNI.
- **Alternatif (red)**: `IInitialData` ile 42 banka — kullanıcı istemiyor; gereksiz veri/bağımlılık.

## Karar 7: Admin banka listesi kaynağı — Commission.Api `/banks`

- **Karar**: Admin, banka dropdown/listesini `GET /banks`'ten alır; `CommissionApiClient`'e bank
  metotları eklenir (yeni HttpClient yok, `commission-api` tekrar kullanılır).
- **Gerekçe**: Bank aggregate Commission.Api'de; ayrı bank-api servisi YAGNI. Mevcut typed-client
  deseni (`MerchantApiClient`/`CommissionApiClient`) korunur.
- **Alternatif (red)**: Admin'de hardcoded banka listesi — çift kaynak, senkron sorunu.