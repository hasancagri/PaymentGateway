using System.Collections.Generic;

namespace Commission.Api.Provider
{
    public class ResponsePagingData<T> : ProviderResourceV2
    {
        public ResponsePaging<T> Data { get; set; }
    }
    
    public class ResponsePaging<T>
    {
        public List<T> Items { get; set; }
        public long? TotalCount { get; set; }
        public int? CurrentPage { get; set; }
        public int? PageCount { get; set; }
    }
}