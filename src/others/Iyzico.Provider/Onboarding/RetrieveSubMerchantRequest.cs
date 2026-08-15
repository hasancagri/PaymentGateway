using System;

namespace Iyzico.Provider.Onboarding
{
    public class RetrieveSubMerchantRequest : BaseRequestV2
    {
        public string SubMerchantExternalId { get; set; }
        public override string ToPKIRequestString()
        {
            return ToStringRequestBuilder.NewInstance()
                .AppendSuper(base.ToPKIRequestString())
                .Append("subMerchantExternalId", SubMerchantExternalId)
                .GetRequestString();
        }
    }
}
