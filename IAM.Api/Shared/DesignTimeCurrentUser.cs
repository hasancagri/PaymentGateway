namespace PaymentGatewayApi.Shared;

internal class DesignTimeCurrentUser : ICurrentUser
{
    public Guid Id { get; set; } = Guid.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
}