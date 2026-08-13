using System.Threading.Tasks;

namespace Payment.Api.Provider.Payments
{
	public class Payment : PaymentResource
	{
		public static Task<Payment> Create(CreatePaymentRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/auth";
			return RestHttpClientV2.Create().PostAsync<Payment>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}

		public static Task<Payment> Retrieve(RetrievePaymentRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/detail";
			return RestHttpClientV2.Create().PostAsync<Payment>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
