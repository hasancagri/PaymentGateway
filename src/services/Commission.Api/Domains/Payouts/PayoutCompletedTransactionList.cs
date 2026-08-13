using System.Collections.Generic;
using System.Threading.Tasks;

namespace Commission.Api.Domains.Payouts
{
    public class PayoutCompletedTransactionList : ProviderResourceV2
    {
        public List<PayoutCompletedTransaction> PayoutCompletedTransactions { get; set; }

        public static Task<PayoutCompletedTransactionList> Retrieve(RetrieveTransactionsRequest request, ProviderOptions options)
        {
            var uri = options.BaseUrl + "/reporting/settlement/payoutcompleted";
            return RestHttpClientV2.Create().PostAsync<PayoutCompletedTransactionList>(uri, GetHttpHeadersWithRequestBody(request, uri, options), request);
        }
    }
}
