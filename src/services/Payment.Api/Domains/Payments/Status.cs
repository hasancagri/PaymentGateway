using System;

namespace Payment.Api.Domains.Payments
{
    public sealed class Status
    {
        private readonly String value;

        public static readonly Status SUCCESS = new Status("success");
        public static readonly Status FAILURE = new Status("failure");

        private Status(String value)
        {
            this.value = value;
        }

        public override String ToString()
        {
            return value;
        } 
    }
}
