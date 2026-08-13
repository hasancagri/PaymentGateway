using System;
using System.Threading.Tasks;

namespace Payment.Api.Domains.Payments
{
	public class Refund : ProviderResourceV2
	{
		public string PaymentId { get; set; }
		public string PaymentTransactionId { get; set; }
		public string Price { get; set; }
		public string Currency { get; set; }
		public string ConnectorName { get; set; }
		public string AuthCode { get; set; }
		public string HostReference { get; set; }

		public static Task<Refund> Create(CreateRefundRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/refund";
			return RestHttpClientV2.Create().PostAsync<Refund>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}

		public static Task<Refund> CreateAmountBasedRefundRequest(CreateAmountBasedRefundRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/v2/payment/refund";
			return RestHttpClientV2.Create().PostAsync<Refund>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}

	}
}
