namespace PaymentGatewayApi.Authorization;

public sealed class JwtPermissionMetadata(string page, string action)
{
    public string Page { get; } = page;
    public string Action { get; } = action;
}