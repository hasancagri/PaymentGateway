using System;

namespace Commission.Api.Domains.TransactionReports
{
    public class TransactionDetail : TransactionDetailResource
    {
        public static TransactionDetail Retrieve(RetrieveTransactionDetailRequest request, ProviderOptions options)
        {
            String url;
            if (String.IsNullOrEmpty(request.PaymentId))
            {
                url = options.BaseUrl
                + "/v2/reporting/payment/details?paymentConversationId="
                + request.PaymentConversationId;
            }
            else
            {
                url = options.BaseUrl
                + "/v2/reporting/payment/details?paymentId="
                + request.PaymentId;
            }
            return RestHttpClientV2.Create().Get<TransactionDetail>(url, GetHttpHeadersWithUrlParams(request, url, options));
        }
    }
}
