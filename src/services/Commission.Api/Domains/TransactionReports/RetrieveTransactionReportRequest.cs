using System;

namespace Commission.Api.Domains.TransactionReports
{
	public class RetrieveTransactionReportRequest : BaseRequestV2
    {
        public String TransactionDate { get; set; }
        public int Page { get; set; }
    }
}