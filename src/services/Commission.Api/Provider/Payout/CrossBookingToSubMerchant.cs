using System.Threading.Tasks;

namespace Commission.Api.Provider.Payout
{
	public class CrossBookingToSubMerchant : ProviderResourceV2
	{
		public static Task<CrossBookingToSubMerchant> Create(CreateCrossBookingRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/crossbooking/send";
			return RestHttpClientV2.Create().PostAsync<CrossBookingToSubMerchant>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
