using System.Collections.Generic;
using System.Threading.Tasks;

namespace Payment.Api.Provider.Installments
{
	public class InstallmentInfo : ProviderResourceV2
	{
		public List<InstallmentDetail> InstallmentDetails { get; set; }

		public static Task<InstallmentInfo> Retrieve(RetrieveInstallmentInfoRequest request, ProviderOptions options)
		{
			var uri = options.BaseUrl + "/payment/iyzipos/installment";
			return RestHttpClientV2.Create().PostAsync<InstallmentInfo>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
		}
	}
}
