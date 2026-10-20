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

    // 088 teardown: MerchantLifecycle / MerchantCommission / MailDelivery fanout'ları SÖKÜLDÜ
    // (Merchant/Commission BC + mail worker öldü).
}
