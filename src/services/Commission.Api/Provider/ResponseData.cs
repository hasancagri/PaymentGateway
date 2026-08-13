namespace Commission.Api.Provider
{
    public class ResponseData<T> : ProviderResourceV2
    {
        public T Data { get; set; }
    }
}