using Common.Utils.Constants;
using Shared;

namespace Merchant.Api.Mcp;

// 047: tool→scope haritası — tools/list budaması (Common McpScopePruningExtension.IsToolVisible)
// buradan okur. Tek gerçek-kaynak [RequiredScope] attribute'larıdır; harita onlarla BİREBİR
// (data-model.md). Haritada OLMAYAN tool herkese görünür (budama kuralı); tüm admin tool'ları
// merchant.admin ister. tools/call son savunması Wolverine ScopeAuthorizationMiddleware'de (043).
public static class MerchantAdminSurface
{
    public static readonly IReadOnlyDictionary<string, string> ToolScopeMap =
        new Dictionary<string, string>
        {
            [MerchantAdminTools.GetMerchants] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.GetMerchant] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.UpdateMerchant] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.ActivateMerchant] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.DeactivateMerchant] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.SuspendMerchant] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.GetPendingRegistrations] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.ApproveRegistration] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.RejectRegistration] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.ResendCredentialLink] = AuthorizationScopes.MerchantAdmin,
            [MerchantAdminTools.RequestSensitiveLink] = AuthorizationScopes.MerchantAdmin,
        };
}
