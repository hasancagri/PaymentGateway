using System;

namespace Commission.Api.Provider.Payout
{
    public class RetrieveTransactionsRequest: BaseRequestV2
    {
        public string Date { get; set; }


        public override string ToPKIRequestString()
        {
            return ToStringRequestBuilder.NewInstance()
                .AppendSuper(base.ToPKIRequestString())
                .Append("date", Date)
                .GetRequestString();
        }
    }
}
