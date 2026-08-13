using System.Collections.Generic;

namespace Commission.Api.Domains.TransactionReports
{
    public class TransactionDetailResource : ProviderResourceV2
    {
        public List<TransactionDetailItem> Payments { get; set; }
    }
}
