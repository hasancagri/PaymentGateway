# Research: Merchant Key

Phase 0 — spec + plan'daki açık kararların çözümü. Tüm "NEEDS CLARIFICATION" burada kapatılır.

## R1: Anahtar tipi — açık kimlik mi, gizli credential mı?

- **Decision**: Açık (public) kimlik. Düz saklanır, her zaman okunabilir, hash yok, tek-seferlik
  gösterim yok.
- **Rationale**: Kullanıcı netleştirdi — key dış sistemlerin merchant'ı *tanımlaması* içindir,
  kimlik *doğrulaması* için değil. Gateway modelinde public merchant identifier'a karşılık gelir
  (merchant id / publishable key). Gizli/hash'li API key ayrı bir kavramdır ve 001 bunu Identity
  dilimine ertelemiştir — bu feature onu geri almaz.
- **Alternatives considered**: Gizli credential (hash + tek-seferlik) — reddedildi; 001'in Identity
  ertelemesiyle çakışır ve kullanıcı amacı (tanımlama) ile örtüşmez.

## R2: Kimlik otoritesi — kim üretir?

- **Decision**: Gateway (sistem) sunucu tarafında üretir. Çağıranın (admin formu/istemci) gönderdiği
  key değeri varsa yok sayılır.
- **Rationale**: PSP/acquirer modeli — merchant kimliğini gateway atar, merchant seçmez. Aksi halde
  benzersizlik ve güven istemciye bırakılır. Bu repoda Merchant = gateway'in registry'si; admin
  onboarding yapar, sistem key'i mint eder.
- **Alternatives considered**: Merchant/istemci key sağlar — reddedildi (benzersizlik/güven riski).

## R3: Key formatı

- **Decision**: `mk_` öneki + `Guid.NewGuid().ToString("N")` (32 hex hane). Örn:
  `mk_9f1c2a7b8d3e4f5061728394a5b6c7d8`. URL-güvenli (yalnız `[a-f0-9_]`), tek parça, boşluksuz,
  sabit uzunluk (35 karakter).
- **Rationale**: Basit, bağımlılıksız, çakışması pratikte imkânsız (122-bit rastgelelik). Önek
  gözle tanınırlık ve log'da ayırt edilebilirlik sağlar. Gizli olmadığından kriptografik
  tahmin-edilemezlik gerekmez; benzersizlik + stabilite yeterli. Proje zaten aggregate içinde
  `Guid`/`DateTime.UtcNow` kullanıyor (saflık katı değil), bu yüzden `Guid.NewGuid` uygun.
- **Alternatives considered**:
  - Ham `Guid` — reddedildi; önek yok, tür belirsiz.
  - `RandomNumberGenerator` + Base32 — reddedildi; gizli olmayan kimlik için fazla mühendislik (YAGNI).
  - Sıralı/okunur slug (merchant-0001) — reddedildi; tahmin edilebilir sıra iş bilgisi sızdırır ve
    yarış koşulunda çakışma yönetimi zor.

## R4: Benzersizlik nasıl garanti edilir?

- **Decision**: Handler'da üret-ve-kontrol döngüsü: üretilen key için `session.Query<Merchant>()
  .AnyAsync(m => m.MerchantKey == candidate)` çakışma yoksa devam; varsa yeniden üret (küçük üst
  sınırlı döngü, ör. 5 deneme). `[Transactional]` handler içinde.
- **Rationale**: Settlement account'un `(MerchantId, Iban)` benzersizliğini handler query'siyle
  denetlemesiyle aynı proje deseni. 122-bit rastgelelikte çakışma astronomik; döngü yalnız
  belt-and-suspenders. DB unique index gerektirmez (proje Marten doküman deseni, mevcut kod
  handler-side kontrol kullanıyor).
- **Alternatives considered**:
  - Marten unique index (`Duplicate`/computed index) — reddedildi; proje mevcut deseni handler-side
    kontrol, ek şema yapılandırması YAGNI. (İleride hacim artarsa index eklenebilir — not düşüldü.)
  - Kontrolsüz (yalnız rastgeleliğe güven) — reddedildi; FR-003 açık benzersizlik garantisi ister.

## R5: Değişmezlik nasıl uygulanır?

- **Decision**: `MerchantKey` private-set; yalnız `Create` fabrikasında atanır. `UpdateProfile`,
  `Activate/Deactivate/Suspend` ve başka hiçbir metot key'e yazmaz. Setter/güncelleme metodu yok.
- **Rationale**: Constitution II — invariant aggregate'te. Yazacak yol olmayınca yapısal olarak
  değişmez.
- **Alternatives considered**: `init` accessor / readonly field — özdeş etki; proje private-set
  konvansiyonunu izliyoruz (tutarlılık).

## R6: Key ile arama (GetMerchantByKey) kapsama girsin mi?

- **Decision**: Evet — P2 query slice'ı. `merchants/by-key/{merchantKey}` GET.
- **Rationale**: Key'i üretip görünür kılmak onu kullanışlı yapar ama gateway'in ilk gerçek ihtiyacı
  "bu key kimin merchant'ı" çözümüdür (ödeme geldiğinde). Küçük, izole, test edilebilir. Kullanıcı
  onayladı.
- **Alternatives considered**: Yalnız alan + gösterim (arama yok) — reddedildi; key'i ölü alan
  bırakır, ayrı dilim maliyeti gereksiz.

## R7: Üretim yeri — aggregate mı handler mı?

- **Decision**: Handler üretir (benzersizlik için session gerekir), aggregate presence + immutability
  zorlar. Key `Merchant.Create(...)`'e parametre girer; aggregate boş/whitespace key'i reddeder.
- **Rationale**: Benzersizlik kontrolü kalıcılık okumasıdır → handler (constitution III/IV). Format/
  presence saf kuraldır → aggregate. Settlement'ın format-aggregate / varlık-handler bölünmesiyle
  birebir aynı.
- **Alternatives considered**: Aggregate `Create` içinde kendi üretir — reddedildi; çakışma yeniden
  üretimi immutable aggregate'te tıkanır, handler'a session lazım.