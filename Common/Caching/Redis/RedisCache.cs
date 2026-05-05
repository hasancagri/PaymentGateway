using System.Text.Json;
using StackExchange.Redis;

namespace Common.Caching.Redis;

public class RedisCache : ICache
{
    private readonly IDatabase? _db;
    private readonly IServer? _server;

    public RedisCache(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return;
        
        var redis = ConnectionMultiplexer.Connect(connectionString);
        _db = redis.GetDatabase();
        var endpoints = redis.GetEndPoints();
        _server = redis.GetServer(endpoints[0]);
        
        redis.ConnectionFailed += (sender, e) =>
        {
            Console.WriteLine("Connection failed: " + e.Exception);
        };
        redis.ConnectionRestored += (sender, e) =>
        {
            Console.WriteLine("Connection restored");
        };
    }
    

    public async Task<TResult?> Get<TResult>(string cacheKey)
    {
        if (_db is null) throw new Exception("db is null");
        var stringResponse = await _db.StringGetAsync(cacheKey);
        return !string.IsNullOrEmpty(stringResponse) ? JsonSerializer.Deserialize<TResult>(stringResponse!) : default(TResult);
    }

    public Task Set<TResult>(string cacheKey, TResult value)
    {
        if (_db is null) throw new Exception("db is null");

        var serializedValue = JsonSerializer.Serialize(value);
        return _db.StringSetAsync(cacheKey, serializedValue);
    }

    public Task Remove(string cacheKey)
    {
        if (_db is null) throw new Exception("db is null");

        return _db.KeyDeleteAsync(cacheKey);
    }

    public Task RemoveByPattern(string pattern)
    {
        if (_db is null) throw new Exception("db is null");

        if (_server is null) throw new Exception("server is null");

        var keys = _server.Keys(pattern: $"{pattern}*", pageSize: 100);
        foreach (var cacheKey in keys)
        {
            _db.KeyDeleteAsync(cacheKey);
        }

        return Task.CompletedTask;
    }
}
