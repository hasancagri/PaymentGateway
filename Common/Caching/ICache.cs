namespace Common.Caching;

public interface ICache
{
    Task<TResult?> Get<TResult>(string cacheKey);
    Task Set<TResult>(string cacheKey, TResult value);
    Task Remove(string cacheKey);
    Task RemoveByPattern(string pattern);
}
