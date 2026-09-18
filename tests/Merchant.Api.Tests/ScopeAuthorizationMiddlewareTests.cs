using System.Security.Claims;
using Common.Utils.Authorization;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Merchant.Api.Tests;

// 043 T006: Wolverine ScopeAuthorizationMiddleware'in PG'de İLK aktivasyonu — Before metodunu
// doğrudan çağırarak (host/HTTP yok, saf birim test) doğru scope'u reddettiğini/geçirdiğini kanıtlar.
public class ScopeAuthorizationMiddlewareTests
{
    [RequiredScope(AuthorizationScopes.MerchantAdmin)]
    private record ScopedTestMessage;

    private static IHttpContextAccessor BuildAccessor(params string[] scopes)
    {
        var identity = new ClaimsIdentity(scopes.Select(s => new Claim("scope", s)));
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextAccessor { HttpContext = httpContext };
    }

    [Fact]
    public void Before_RequiredScopeYokken_UnauthorizedAccessExceptionFirlatir()
    {
        var envelope = new Envelope(new ScopedTestMessage());
        var accessor = BuildAccessor("merchant.read", "merchant.write");

        Assert.Throws<UnauthorizedAccessException>(
            () => ScopeAuthorizationMiddleware.Before(envelope, accessor));
    }

    [Fact]
    public void Before_RequiredScopeVarken_SessizceGecer()
    {
        var envelope = new Envelope(new ScopedTestMessage());
        var accessor = BuildAccessor("merchant.write", AuthorizationScopes.MerchantAdmin);

        var exception = Record.Exception(() => ScopeAuthorizationMiddleware.Before(envelope, accessor));

        Assert.Null(exception);
    }
}
