namespace ApiGateway;

public class JwtOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "payment-gateway";
    public string Audience { get; set; } = "payment-gateway-services";
    public int ExpiryMinutes { get; set; } = 15;
}