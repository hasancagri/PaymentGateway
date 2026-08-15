using System;

namespace Iyzico.Provider
{
    public class PagingRequest : BaseRequestV2
    {
        public int? Page { get; set; }
        public int? Count { get; set; }
        
    }
}