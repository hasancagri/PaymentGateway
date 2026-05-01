using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Common.Caching;

public static class CacheKeyGenerator
{
    public static string Generate<TQuery>(TQuery query, string prefix)
    {
        var json = JsonSerializer.Serialize(query);
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(json));
        return $"{prefix}_{Convert.ToHexString(hash)}";
    }
}