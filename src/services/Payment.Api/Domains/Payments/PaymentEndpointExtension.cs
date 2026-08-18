using Payment.Api.Domains.Payments.Features.Commands;
using Payment.Api.Domains.Payments.Features.Queries;

namespace Payment.Api.Domains.Payments;

/// <summary>
/// 033: kayıtlı kartla ödeme uç grubu: <c>.../merchants/{merchantId}/payments</c>. Charge +
/// taksit sorgusu; ikisi de <c>payment.charge</c> (Active merchant) + <c>MerchantScoped</c>.
/// </summary>
public static class PaymentEndpointExtension
{
    public static void AddPaymentGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchants/{merchantId:guid}/payments")
            .WithTags("payments")
            .WithApiVersionSet(apiVersionSet)
            .ChargePaymentGroupItemEndpoint()
            .RetrievePaymentGroupItemEndpoint()
            .InstallmentOptionsGroupItemEndpoint();
    }
}
