using Merchant.Api.Domains.Merchants;

namespace Merchant.Api.Tests;

// 048 test-first (Domain-TDD, İLKE VI): hassas-veri hosted link'inin yetkisini temsil eden
// süreli + tek-kullanımlık oturum — saf domain. INV-1..INV-7 (data-model.md).
public class SensitiveEntrySessionTests
{
    private static readonly Guid MerchantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    // INV-1: Create boş merchantId/userId veya ≤0 lifetime'ı reddeder.
    [Fact]
    public void Create_BosMerchantId_Error()
    {
        Assert.False(SensitiveEntrySession.Create(Guid.Empty, UserId, Lifetime).IsSuccess);
    }

    [Fact]
    public void Create_BosUserId_Error()
    {
        Assert.False(SensitiveEntrySession.Create(MerchantId, Guid.Empty, Lifetime).IsSuccess);
    }

    [Fact]
    public void Create_PozitifOlmayanSure_Error()
    {
        Assert.False(SensitiveEntrySession.Create(MerchantId, UserId, TimeSpan.Zero).IsSuccess);
    }

    // INV-2: Create sonrası IsUsable(now) true (now ≤ ExpiresAt).
    [Fact]
    public void Create_Gecerli_OkVeUsable()
    {
        var before = DateTimeOffset.UtcNow;
        var result = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime);

        Assert.True(result.IsSuccess);
        var session = result.Data!;
        Assert.Equal(MerchantId, session.MerchantId);
        Assert.Equal(UserId, session.RequestedByUserId);
        Assert.Null(session.ConsumedAt);
        Assert.True(session.IsUsable(DateTimeOffset.UtcNow));
        Assert.True(session.ExpiresAt >= before + Lifetime);
        Assert.True(session.ExpiresAt <= DateTimeOffset.UtcNow + Lifetime);
    }

    // INV-3: IsUsable süresi geçmişte (now > ExpiresAt) false.
    [Fact]
    public void IsUsable_SuresiGecmis_False()
    {
        var session = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!;

        Assert.True(session.IsUsable(DateTimeOffset.UtcNow));
        Assert.False(session.IsUsable(session.ExpiresAt.AddSeconds(1)));
    }

    // INV-4: Consume usable oturumu tüketir, ConsumedAt dolar.
    [Fact]
    public void Consume_MutluYol_OkVeConsumedAtDolar()
    {
        var session = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;

        Assert.True(session.Consume(now).IsSuccess);
        Assert.Equal(now, session.ConsumedAt);
        Assert.False(session.IsUsable(now));
    }

    // INV-5: Tüketilmiş oturumda ikinci Consume → Error (tek kullanım).
    [Fact]
    public void Consume_IkinciKez_ErrorIlkAnKorunur()
    {
        var session = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!;
        var now = DateTimeOffset.UtcNow;
        Assert.True(session.Consume(now).IsSuccess);

        Assert.False(session.Consume(now.AddSeconds(1)).IsSuccess);
        Assert.Equal(now, session.ConsumedAt);
    }

    // INV-6: Süresi geçmiş oturumda Consume → Error.
    [Fact]
    public void Consume_SuresiGecmis_Error()
    {
        var session = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!;

        Assert.False(session.Consume(session.ExpiresAt.AddSeconds(1)).IsSuccess);
        Assert.Null(session.ConsumedAt);
    }

    // INV-7: Token her Create'te farklı + URL-safe (base64url charset).
    [Fact]
    public void Create_TokenHerSeferindeFarkliVeUrlSafe()
    {
        var a = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!.Token;
        var b = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!.Token;

        Assert.NotEqual(a, b);
        // 256-bit = 32 bayt → base64url 43 karakter; +, / ve = URL'e giremez.
        Assert.True(a.Length >= 43);
        Assert.DoesNotContain("+", a);
        Assert.DoesNotContain("/", a);
        Assert.DoesNotContain("=", a);
    }

    // T011/SC-004: nötr-red kararı — endpoint her iki uçta tek IsUsable dallanmasıyla nötr 404 verir.
    // "Bilinmeyen" token = DB'de kayıt yok (endpoint entry is null → aynı NotFoundPage). Var olan üç
    // ölü durum (süresi geçmiş + tüketilmiş) IsUsable(now)==false döner → süreli/tüketilmiş/bilinmeyen
    // ayırt edilemez (aynı gövde+status by construction). Bu test var-olan-ölü iki yolu doğrular.
    [Fact]
    public void IsUsable_SureliVeTuketilmis_IkisiDeFalse_NotrRed()
    {
        var now = DateTimeOffset.UtcNow;

        // (a) süresi geçmiş oturum
        var expired = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!;
        Assert.False(expired.IsUsable(expired.ExpiresAt.AddSeconds(1)));

        // (b) tüketilmiş oturum
        var consumed = SensitiveEntrySession.Create(MerchantId, UserId, Lifetime).Data!;
        Assert.True(consumed.Consume(now).IsSuccess);
        Assert.False(consumed.IsUsable(now));

        // İki ölü durum aynı kararı verir (endpoint her ikisini de nötr 404'e eşler).
        Assert.Equal(expired.IsUsable(now.AddHours(1)), consumed.IsUsable(now.AddHours(1)));
    }
}
