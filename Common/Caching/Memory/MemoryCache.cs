using System.Text.RegularExpressions;
using Common.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Common;

public class MemoryCache : ICache

{
    private static List<string> _keys = new();
    private readonly IMemoryCache _memoryCache;

    public MemoryCache(
        IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public Task<TResponse?> Get<TResponse>(string cacheKey)
    {
        return _memoryCache.TryGetValue(cacheKey, out var cachedResponse)
            ? Task.FromResult((TResponse)cachedResponse)
            : Task.FromResult(default(TResponse));
    }
    
    public Task Set<TResponse>(string cacheKey, TResponse value)
    {
        _keys.Add(cacheKey);
        _memoryCache.Set(cacheKey, value);
        return Task.CompletedTask;
    }

    public Task Remove(string cacheKey)
    {
        _keys.Remove(cacheKey);
        _memoryCache.Remove(cacheKey);
        return Task.CompletedTask;
    }

    public Task RemoveByPattern(string pattern)
    {
        var keysToRemove = _keys
            .Where(k => Regex.IsMatch(k, pattern, RegexOptions.IgnoreCase))
            .ToArray();

        foreach (var cacheKey in keysToRemove)
        {
            _memoryCache.Remove(cacheKey);
        }

        return Task.CompletedTask;
    }
}
