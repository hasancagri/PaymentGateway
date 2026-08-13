using System;

namespace Payment.Api.Provider
{
    public class ProviderOptions
    {
        public String ApiKey { get; set; }
        public String SecretKey { get; set; }
        public String BaseUrl { get; set; }
    }
}
