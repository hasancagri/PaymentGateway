using System.Security.Claims;

namespace Common.Extensions;

// 047 R4: EC 085 McpScopePruningExtension'ın bilinçli kopyası (repo bağlaşması yerine tekrar;
// conventions "bilinçli tekrar"). Tek gerçek-kaynak BC'nin *AdminSurface.ToolScopeMap holder'ı;
// budama yol-prefix yerine oturumu açan token'ın scope claim'lerine bakar. Fasat süzmez —
// downstream olarak PG kendi budanmış listesini döner (contracts/mcp-surface.md).
public static class McpScopePruningExtension
{
    // tool ∈ liste ⟺ tool ∉ adminToolScopes ∨ requiredScope ∈ token.scopes — scope başına, hep-ya-hiç DEĞİL.
    // Token'sız/anonim kullanıcı (ClaimsPrincipal boş) haritadaki hiçbir admin tool'u göremez.
    public static bool IsToolVisible(string toolName, IReadOnlyDictionary<string, string> adminToolScopes, ClaimsPrincipal user)
        => !adminToolScopes.TryGetValue(toolName, out var requiredScope) || user.HasClaim("scope", requiredScope);
}
