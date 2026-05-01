using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace PaymentGatewayPortal.Auth;

public class PortalAuthStateProvider(
    ProtectedSessionStorage sessionStorage,
    JwtTokenStore tokenStore) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var result = await sessionStorage.GetAsync<string>("jwt");
            if (!result.Success || string.IsNullOrEmpty(result.Value))
                return new AuthenticationState(Anonymous);

            tokenStore.Token = result.Value;
            var principal = BuildPrincipal(result.Value);
            return new AuthenticationState(principal);
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    public async Task SignInAsync(string token)
    {
        await sessionStorage.SetAsync("jwt", token);
        tokenStore.Token = token;
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(BuildPrincipal(token))));
    }

    public async Task SignOutAsync()
    {
        await sessionStorage.DeleteAsync("jwt");
        tokenStore.Token = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    private static ClaimsPrincipal BuildPrincipal(string token)
    {
        var claims = ParseClaims(token);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
    }

    private static IEnumerable<Claim> ParseClaims(string token)
    {
        var payload = token.Split('.')[1];
        var padded = payload.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        return dict.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()));
    }
}