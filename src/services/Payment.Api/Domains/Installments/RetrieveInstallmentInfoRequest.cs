using System;

namespace Payment.Api.Domains.Installments
{
    public class RetrieveInstallmentInfoRequest : BaseRequestV2
    {
        public string BinNumber { get; set; }
        public string Price { get; set; }

        public override string ToPKIRequestString()
        {
            return ToStringRequestBuilder.NewInstance()
                .AppendSuper(base.ToPKIRequestString())
                .Append("binNumber", BinNumber)
                .AppendPrice("price", Price)
                .GetRequestString();
        }
    }
}
