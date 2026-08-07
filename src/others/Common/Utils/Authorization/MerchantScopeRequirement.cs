using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Common.Utils.Authorization;

// 012 (D7): claim-vs-route tenant kontrolü. Karar mantığı MerchantScopeEvaluator'da (saf);
// handler yalnız claim + route değerini toplar. Ret → 403 (authenticated + yetkisiz).
public sealed class MerchantScopeRequirement : IAuthorizationRequirement;

// Yalnız merchant_id claim'i taşımayan (statik istemci) token'lar geçer — admin düzlemi uçları.
public sealed class AdminPlaneOnlyRequirement : IAuthorizationRequirement;

public sealed class MerchantScopeAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<MerchantScopeRequirement>
{
    public const string MerchantIdClaim = "merchant_id";
    public const string MerchantIdRouteKey = "merchantId";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, MerchantScopeRequirement requirement)
    {
        var merchantIdClaim = context.User.FindFirst(MerchantIdClaim)?.Value;
        var routeMerchantId = httpContextAccessor.HttpContext?.GetRouteValue(MerchantIdRouteKey)?.ToString();

        if (MerchantScopeEvaluator.IsAllowed(merchantIdClaim, routeMerchantId))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public sealed class AdminPlaneOnlyAuthorizationHandler : AuthorizationHandler<AdminPlaneOnlyRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AdminPlaneOnlyRequirement requirement)
    {
        if (context.User.FindFirst(MerchantScopeAuthorizationHandler.MerchantIdClaim) is null)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}