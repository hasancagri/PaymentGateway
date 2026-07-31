namespace Shared;

public static class IntegrationEvents
{
    // Ödeme sonucu event'leri: Payment yayınlar; Order BC geldiğinde tüketecek.
    public record PaymentCompletedEvent(Guid PaymentId, string OrderNumber, decimal Amount, string BankCode);

    public record PaymentFailedEvent(Guid PaymentId, string OrderNumber, decimal Amount, string? Reason);
}