using System.Threading.Tasks;

namespace Commission.Api.Provider.Payout
{
	public class CrossBookingFromSubMerchant : ProviderResourceV2
	{
		public static Task<CrossBookingFromSubMerchant> Create(CreateCrossBookingRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/crossbooking/receive";
			return RestHttpClientV2.Create().PostAsync<CrossBookingFromSubMerchant>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
