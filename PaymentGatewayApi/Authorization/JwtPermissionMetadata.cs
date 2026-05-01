namespace PaymentGatewayApi.Authorization;

public sealed class JwtPermissionMetadata(string permission)
{
    public string Permission { get; } = permission;
}