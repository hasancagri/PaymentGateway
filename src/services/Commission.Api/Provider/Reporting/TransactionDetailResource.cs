using System.Collections.Generic;

namespace Commission.Api.Provider.Reporting
{
    public class TransactionDetailResource : ProviderResourceV2
    {
        public List<TransactionDetailItem> Payments { get; set; }
    }
}
