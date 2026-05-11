namespace IAM.Api.Domains.Users.ValueObjects;

public sealed record PasswordHash
{
    public string Hash { get; }
    [Newtonsoft.Json.JsonConstructor]
    private PasswordHash(string hash) => Hash = hash;

    public static ResultDomain<PasswordHash> Create(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 8)
            return ResultDomain<PasswordHash>.Error(new MessageItem { Code = "PasswordHash.TooShort" });

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainPassword)));
        return ResultDomain<PasswordHash>.Ok(new PasswordHash(hash));
    }

    public static PasswordHash FromHash(string hash) => new(hash);

    public bool Verify(string plainPassword) =>
        Hash == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainPassword)));
}