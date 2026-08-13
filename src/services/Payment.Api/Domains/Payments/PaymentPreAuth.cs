using System.Threading.Tasks;

namespace Payment.Api.Domains.Payments
{
	public class PaymentPreAuth : PaymentResource
	{
		public static Task<PaymentPreAuth> Create(CreatePaymentRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/preauth";
			return RestHttpClientV2.Create().PostAsync<PaymentPreAuth>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}

		public static Task<PaymentPreAuth> Retrieve(RetrievePaymentRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/detail";
			return RestHttpClientV2.Create().PostAsync<PaymentPreAuth>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
