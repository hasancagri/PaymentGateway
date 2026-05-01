namespace Common.Caching.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CacheResultAttribute(string prefix) : Attribute
{
    public string Prefix { get; } = prefix;
}