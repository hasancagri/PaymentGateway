using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Iyzico.Provider.Payout
{
	public class BouncedBankTransferList : ProviderResourceV2
	{
		[JsonProperty(PropertyName = "bouncedRows")]
		public List<BankTransfer> BankTransfers { get; set; }

		public static Task<BouncedBankTransferList> Retrieve(RetrieveTransactionsRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/reporting/settlement/bounced";
			return RestHttpClientV2.Create().PostAsync<BouncedBankTransferList>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
