using Common.Utils.Authorization;
using Xunit;

namespace Merchant.Api.Tests;

// 012: tenant erişim kararının saf çekirdeği (data-model §5 karar tablosu).
public class MerchantScopeEvaluatorTests
{
    private const string MerchantId = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
    private const string OtherMerchantId = "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d";

    [Fact]
    public void Claim_yoksa_izin_verilir_admin_ve_agent_duzlemi()
    {
        Assert.True(MerchantScopeEvaluator.IsAllowed(null, MerchantId));
        Assert.True(MerchantScopeEvaluator.IsAllowed(null, null));
        Assert.True(MerchantScopeEvaluator.IsAllowed("", MerchantId));
        Assert.True(MerchantScopeEvaluator.IsAllowed("   ", null));
    }

    [Fact]
    public void Claim_var_route_yoksa_ret_fail_closed()
    {
        Assert.False(MerchantScopeEvaluator.IsAllowed(MerchantId, null));
        Assert.False(MerchantScopeEvaluator.IsAllowed(MerchantId, ""));
        Assert.False(MerchantScopeEvaluator.IsAllowed(MerchantId, "   "));
    }

    [Fact]
    public void Claim_route_esitse_izin_verilir()
    {
        Assert.True(MerchantScopeEvaluator.IsAllowed(MerchantId, MerchantId));
    }

    [Fact]
    public void Claim_route_farkliysa_ret()
    {
        Assert.False(MerchantScopeEvaluator.IsAllowed(MerchantId, OtherMerchantId));
    }

    [Fact]
    public void Esitlik_buyuk_kucuk_harf_ve_bosluk_duyarsiz()
    {
        Assert.True(MerchantScopeEvaluator.IsAllowed(MerchantId.ToUpperInvariant(), MerchantId));
        Assert.True(MerchantScopeEvaluator.IsAllowed($"  {MerchantId}  ", MerchantId));
    }
}