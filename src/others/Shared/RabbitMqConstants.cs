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
}