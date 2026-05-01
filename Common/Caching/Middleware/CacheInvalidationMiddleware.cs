using Common.Caching.Attributes;
using Wolverine;

namespace Common.Caching.Middleware;

public class CacheInvalidationMiddleware(ICacheManager cache)
{
    public async Task AfterAsync(Envelope envelope)
    {
        var message = envelope.Message;
        if (message is null) return;

        var attr = message.GetType().GetCustomAttribute<CacheResultAttribute>();
        if (attr is null) return;

        foreach (var entity in attr.Prefix.Split('_', StringSplitOptions.RemoveEmptyEntries))
            await cache.RemoveByPattern(entity);
    }
}