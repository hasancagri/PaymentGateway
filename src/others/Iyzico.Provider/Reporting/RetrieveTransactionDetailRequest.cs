using System;

namespace Iyzico.Provider.Reporting
{
    public class RetrieveTransactionDetailRequest : BaseRequestV2
    {
        public String PaymentConversationId { get; set; }
        public String PaymentId { get; set; }
    }
}