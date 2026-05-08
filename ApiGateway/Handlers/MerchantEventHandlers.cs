using PaymentGateway.SharedContracts.MerchantEvents;
using StackExchange.Redis;

namespace ApiGateway.Handlers;

public static class MerchantEventHandlers
{
    // Stores merchant name for JWT payload
    public static async Task Handle(MerchantCreated evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        await db.HashSetAsync($"merchant:{evt.MerchantId}", [
            new HashEntry("name", evt.Name),
            new HashEntry("status", "Active")
        ]);
    }

    // Stores api_key_hash → merchant_id + name mapping used at validation time
    public static async Task Handle(ApiKeyGenerated evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        var merchantName = await db.HashGetAsync($"merchant:{evt.MerchantId}", "name");
        await db.HashSetAsync($"apikey:{evt.KeyHash}", [
            new HashEntry("merchant_id", evt.MerchantId.ToString()),
            new HashEntry("merchant_name", merchantName.HasValue ? merchantName.ToString() : string.Empty),
            new HashEntry("status", "Active")
        ]);
    }

    public static async Task Handle(ApiKeyRevoked evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"apikey:{evt.KeyHash}");
    }

    public static async Task Handle(MerchantStatusChanged evt, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        await db.HashSetAsync($"merchant:{evt.MerchantId}", "status", evt.NewStatus);
    }
}