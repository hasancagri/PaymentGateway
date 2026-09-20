namespace Merchant.Api.Tests;

// 046 US1 test-first (İLKE VI): Merchant.ReissueKey saf domain davranışı — Active guard + taze key.
// Senaryo: kaybını bildiren aktif merchant taze key alır; pasif merchant'ın key'i yanlışlıkla değişmez.
public class MerchantReissueKeyTests
{
    private const string ValidIban = "TR330006100519786457841326";

    private static Domains.Merchants.Merchant CreateActive()
        => Domains.Merchants.Merchant.Create(
            MerchantType.Personal, "Kolay Fırsat", "iletisim@kolayfirsat.com", "+905551112233",
            "İstanbul", ValidIban, "Ahmet", "Yılmaz", "11111111110", null, null, null).Data!;

    [Fact]
    public void ReissueKey_ActiveMerchant_YeniMkPrefiksliFarkliKeyUretir()
    {
        var merchant = CreateActive();
        var oldKey = merchant.MerchantKey;

        var result = merchant.ReissueKey();

        Assert.True(result.IsSuccess);
        Assert.StartsWith("mk_", merchant.MerchantKey);
        Assert.NotEqual(oldKey, merchant.MerchantKey);
    }

    [Fact]
    public void ReissueKey_PasifMerchant_ErrorVeKeyDegismez()
    {
        var merchant = CreateActive();
        merchant.ChangeStatus(MerchantStatus.Passive);
        var keyBefore = merchant.MerchantKey;

        var result = merchant.ReissueKey();

        Assert.False(result.IsSuccess);
        Assert.Equal(keyBefore, merchant.MerchantKey);
    }

    [Fact]
    public void ReissueKey_SuspendedMerchant_ErrorVeKeyDegismez()
    {
        var merchant = CreateActive();
        merchant.ChangeStatus(MerchantStatus.Suspended);
        var keyBefore = merchant.MerchantKey;

        var result = merchant.ReissueKey();

        Assert.False(result.IsSuccess);
        Assert.Equal(keyBefore, merchant.MerchantKey);
    }

    [Fact]
    public void ReissueKey_ArdisikCagri_HerSeferindeFarkliKey()
    {
        var merchant = CreateActive();
        var k0 = merchant.MerchantKey;
        merchant.ReissueKey();
        var k1 = merchant.MerchantKey;
        merchant.ReissueKey();
        var k2 = merchant.MerchantKey;

        Assert.NotEqual(k0, k1);
        Assert.NotEqual(k1, k2);
    }
}