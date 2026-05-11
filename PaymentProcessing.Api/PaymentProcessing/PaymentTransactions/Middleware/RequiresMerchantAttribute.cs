namespace PaymentProcessing.Api.Modules.PaymentProcessing.PaymentTransactions.Middleware;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresMerchantAttribute : Attribute { }