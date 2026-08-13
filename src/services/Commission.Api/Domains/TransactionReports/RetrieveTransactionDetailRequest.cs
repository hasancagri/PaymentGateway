using System;

namespace Commission.Api.Domains.TransactionReports
{
    public class RetrieveTransactionDetailRequest : BaseRequestV2
    {
        public String PaymentConversationId { get; set; }
        public String PaymentId { get; set; }
    }
}