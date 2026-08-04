namespace Shared;

public static class IntegrationEvents
{
    // Ödeme sonucu event'leri: Payment yayınlar; Order BC geldiğinde tüketecek.
    public record PaymentCompletedEvent(Guid PaymentId, string OrderNumber, decimal Amount, string BankCode);

    public record PaymentFailedEvent(Guid PaymentId, string OrderNumber, decimal Amount, string? Reason);

    // Referans veri güncellemesi: Reference.Api yayınlar; Merchant + Commission tüketir (yerel read-model upsert).
    // Kind ∈ { "Country", "City", "Mcc", "Bank" }. Tam-set (seed) veya diff (büyütme) taşınabilir.
    public record ReferenceDataUpdated(string Kind, IReadOnlyList<ReferenceItem> Items);

    // CountryCode yalnız City için dolu; diğer türlerde null.
    public record ReferenceItem(string Code, string Name, string? CountryCode);
}