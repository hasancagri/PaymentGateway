using Microsoft.Extensions.Logging;

namespace Common.Caching;

public class CacheManager : ICacheManager
{
    private readonly List<ICache> _caches;
    private readonly ILogger<CacheManager> _logger;

    public CacheManager(IServiceProvider serviceProvider, ILogger<CacheManager> logger)
    {
        _logger = logger;
        _caches = serviceProvider.GetServices<ICache>().ToList();
    }

    private string GetCacheKey(string cacheKey)
    {
        var prefix = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return $"{prefix}_{cacheKey}";
    }

    public async Task<TResult?> Get<TResult>(string cacheKey)
    {
        var prefixCacheKey = GetCacheKey(cacheKey);
        var emptyCaches = new List<ICache>();

        foreach (var cache in _caches)
        {
            try
            {
                var cachedValue = await cache.Get<TResult>(prefixCacheKey);
                if (cachedValue != null)
                {
                    foreach (var emptyCache in emptyCaches)
                    {
                        await emptyCache.Set(prefixCacheKey, cachedValue);
                    }

                    return cachedValue;
                }

                emptyCaches.Add(cache);
            }
            catch (Exception e)
            {
                var cacheType = cache.GetType();
                _logger.LogError(e, $"An error occurred while get/set cache {cacheType} - {prefixCacheKey}");
            }
        }

        return default(TResult);
    }

    public Task Set<TResult>(string cacheKey, TResult value)
    {
        var prefixCacheKey = GetCacheKey(cacheKey);
        foreach (var cache in _caches)
        {
            try
            {
                cache.Set(prefixCacheKey, value);
            }
            catch (Exception e)
            {
                var cacheType = cache.GetType();
                _logger.LogError(e, $"An error occurred while set cache {cacheType} - {prefixCacheKey}");
            }
        }

        return Task.CompletedTask;
    }

    public Task Remove(string cacheKey)
    {
        var prefixCacheKey = GetCacheKey(cacheKey);
        foreach (var cache in _caches)
        {
            try
            {
                cache.Remove(prefixCacheKey);
            }
            catch (Exception e)
            {
                var cacheType = cache.GetType();
                _logger.LogError(e, $"An error occurred while remove cache {cacheType} - {prefixCacheKey}");
            }
        }

        return Task.CompletedTask;
    }

    public Task RemoveByPattern(string pattern)
    {
        var prefixCacheKey = GetCacheKey(pattern);
        foreach (var cache in _caches)
        {
            try
            {
                cache.RemoveByPattern(prefixCacheKey);
            }
            catch (Exception e)
            {
                var cacheType = cache.GetType();
                _logger.LogError(e, $"An error occurred while remove pattern cache {cacheType} - {prefixCacheKey}");
            }
        }

        return Task.CompletedTask;
    }
}