using System.Security.Claims;
using Common.Extensions;
using Merchant.Api.Mcp;

namespace Merchant.Api.Tests;

// 047 İLKE VI test-first: saf budama kuralı (McpScopePruningExtension.IsToolVisible). Kimlik/endpoint
// wiring canlı doğrulanır (quickstart S1-S4); burada yalnız görünürlük mantığı.
public class ScopePruningTests
{
    private static ClaimsPrincipal User(params string[] scopes)
        => new(new ClaimsIdentity(scopes.Select(s => new Claim("scope", s)), authenticationType: "test"));

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    [Fact]
    public void HaritadaOlmayanTool_HerkeseGorunur()
    {
        // "unknown_tool" ToolScopeMap'te yok → scope aranmaz, anonim kullanıcıya bile görünür.
        Assert.True(McpScopePruningExtension.IsToolVisible(
            "unknown_tool", MerchantAdminSurface.ToolScopeMap, Anonymous()));
    }

    [Fact]
    public void HaritadakiTool_ScopeVarsaGorunur()
    {
        Assert.True(McpScopePruningExtension.IsToolVisible(
            Shared.MerchantAdminTools.GetMerchants, MerchantAdminSurface.ToolScopeMap,
            User(AuthorizationScopes.MerchantAdmin)));
    }

    [Fact]
    public void AnonimKullanici_AdminToolGoremez()
    {
        Assert.False(McpScopePruningExtension.IsToolVisible(
            Shared.MerchantAdminTools.GetMerchants, MerchantAdminSurface.ToolScopeMap, Anonymous()));
    }

    [Fact]
    public void ClaimsizKullanici_AdminToolGoremez()
    {
        // Kimlikli ama scope'suz token — admin tool budanır (hep-ya-hiç değil, scope başına).
        Assert.False(McpScopePruningExtension.IsToolVisible(
            Shared.MerchantAdminTools.UpdateMerchant, MerchantAdminSurface.ToolScopeMap, User()));
    }

    [Fact]
    public void KismiScope_YalnizKendiToolunuGorur()
    {
        // Yalnız commission.read taşıyan token: okuma tool'u görünür, yazma tool'u görünmez,
        // merchant tool'u görünmez (scope başına budama; FR-002 hep-ya-hiç değil).
        var map = new Dictionary<string, string>
        {
            [Shared.CommissionAdminTools.GetCommissionPolicy] = AuthorizationScopes.CommissionRead,
            [Shared.CommissionAdminTools.CreatePolicy] = AuthorizationScopes.CommissionWrite,
        };
        var user = User(AuthorizationScopes.CommissionRead);

        Assert.True(McpScopePruningExtension.IsToolVisible(
            Shared.CommissionAdminTools.GetCommissionPolicy, map, user));
        Assert.False(McpScopePruningExtension.IsToolVisible(
            Shared.CommissionAdminTools.CreatePolicy, map, user));
        Assert.False(McpScopePruningExtension.IsToolVisible(
            Shared.MerchantAdminTools.GetMerchants, MerchantAdminSurface.ToolScopeMap, user));
    }
}
