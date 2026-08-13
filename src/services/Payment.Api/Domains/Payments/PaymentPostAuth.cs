using System.Threading.Tasks;

namespace Payment.Api.Domains.Payments
{
	public class PaymentPostAuth : PaymentResource
	{
		public static Task<PaymentPostAuth> Create(CreatePaymentPostAuthRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/postauth";
			return RestHttpClientV2.Create().PostAsync<PaymentPostAuth>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
