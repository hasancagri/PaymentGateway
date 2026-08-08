namespace Shared;

public static class RabbitMqConstants
{
    public static class PaymentCompleted
    {
        public const string Exchange = "payment.completed";
    }

    public static class PaymentFailed
    {
        public const string Exchange = "payment.failed";
    }

    public static class ReferenceDataUpdated
    {
        public const string Exchange = "reference.data-updated";
    }

    public static class MerchantLifecycle
    {
        public const string Exchange = "merchant.lifecycle";
        public const string IdentityQueue = "identity.merchant-sync";
    }

    // 013: komisyon grid-hazır fanout. Commission.Api yayınlar; Merchant.Api durable queue ile tüketir
    // (Active koşulu #2 — MerchantCommissionGridReady).
    public static class MerchantCommission
    {
        public const string Exchange = "merchant.commission";
        public const string MerchantQueue = "merchant.commission-ready";
    }
}