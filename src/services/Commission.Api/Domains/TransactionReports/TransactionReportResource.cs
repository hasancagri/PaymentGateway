using System.Collections.Generic;

namespace Commission.Api.Domains.TransactionReports
{
    public class TransactionReportResource : ProviderResourceV2
    {
        public int? CurrentPage { get; set; }
        public int? TotalPageCount { get; set; }
        public List<TransactionReportItem> Transactions { get; set; }
    }
}
