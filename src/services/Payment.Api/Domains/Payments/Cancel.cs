using System;
using System.Threading.Tasks;

namespace Payment.Api.Domains.Payments
{
	public class Cancel : ProviderResourceV2
	{
		public string PaymentId { get; set; }
		public string Price { get; set; }
		public string Currency { get; set; }
		public string ConnectorName { get; set; }
		public string AuthCode { get; set; }
		public string HostReference { get; set; }

		public static Task<Cancel> Create(CreateCancelRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/cancel";
			return RestHttpClientV2.Create().PostAsync<Cancel>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
