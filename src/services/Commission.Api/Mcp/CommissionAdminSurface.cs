using Common.Utils.Constants;
using Shared;

namespace Commission.Api.Mcp;

// 047: tool→scope haritası — tools/list budaması (Common McpScopePruningExtension.IsToolVisible)
// buradan okur. Tek gerçek-kaynak [RequiredScope] attribute'larıdır; harita onlarla BİREBİR
// (data-model.md): okuma commission.read, yazma commission.write. tools/call son savunması
// Wolverine ScopeAuthorizationMiddleware'de (044).
public static class CommissionAdminSurface
{
    public static readonly IReadOnlyDictionary<string, string> ToolScopeMap =
        new Dictionary<string, string>
        {
            [CommissionAdminTools.GetCommissionPolicy] = AuthorizationScopes.CommissionRead,
            [CommissionAdminTools.CreatePolicy] = AuthorizationScopes.CommissionWrite,
            [CommissionAdminTools.UpdateMargin] = AuthorizationScopes.CommissionWrite,
            [CommissionAdminTools.ChangeStatus] = AuthorizationScopes.CommissionWrite,
        };
}
