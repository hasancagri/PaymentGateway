using System.Collections.Generic;

namespace Iyzico.Provider.Reporting
{
    public class TransactionReportResource : ProviderResourceV2
    {
        public int? CurrentPage { get; set; }
        public int? TotalPageCount { get; set; }
        public List<TransactionReportItem> Transactions { get; set; }
    }
}
