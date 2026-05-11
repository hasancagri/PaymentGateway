namespace PaymentProcessing.Api.PaymentProcessing.PaymentTransactions.Middleware;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresMerchantAttribute : Attribute { }