using Common.Caching;
using Microsoft.Extensions.DependencyInjection;

namespace Common;

public static class RedisCacheExtensions
{
    public static void AddRedisCache(this IServiceCollection serviceCollection, string? connectionString)
    {
        var redisCache = new RedisCache(connectionString);

        serviceCollection.AddSingleton<ICache>(redisCache);
    }
}
