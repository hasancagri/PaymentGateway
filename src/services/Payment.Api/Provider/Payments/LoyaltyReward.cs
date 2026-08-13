using System;

namespace Payment.Api.Provider.Payments
{
    public class LoyaltyReward : RequestStringConvertible
    {
        public String RewardAmount { get; set; }
        public int RewardUsage { get; set; }

        public virtual String ToPKIRequestString()
        {
            return ToStringRequestBuilder.NewInstance()
                .Append("rewardAmount", RewardAmount)
                .Append("rewardUsage", RewardUsage)
                .GetRequestString();
        }
    }
}
