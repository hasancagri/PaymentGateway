namespace Common.Caching;

public static class CachingExtensions
{
    public static void AddCachingServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICache, MemoryCache>();
        services.AddSingleton<ICacheManager, CacheManager>();
    }
}