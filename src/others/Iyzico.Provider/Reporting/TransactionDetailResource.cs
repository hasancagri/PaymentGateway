using System.Collections.Generic;

namespace Iyzico.Provider.Reporting
{
    public class TransactionDetailResource : ProviderResourceV2
    {
        public List<TransactionDetailItem> Payments { get; set; }
    }
}
