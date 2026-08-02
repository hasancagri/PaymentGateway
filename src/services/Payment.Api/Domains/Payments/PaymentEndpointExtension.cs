using Payment.Api.Domains.Payments.Features.Commands;
using Payment.Api.Domains.Payments.Features.Queries;

namespace Payment.Api.Domains.Payments;

public static class PaymentEndpointExtension
{
    public static void AddPaymentGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/payments").WithTags("payments").WithApiVersionSet(apiVersionSet)
            .ProcessPaymentGroupItemEndpoint()
            .CompletePayment3DGroupItemEndpoint()
            .CancelPaymentGroupItemEndpoint()
            .RefundPaymentGroupItemEndpoint()
            .GetInstallmentOptionsGroupItemEndpoint();
    }
}