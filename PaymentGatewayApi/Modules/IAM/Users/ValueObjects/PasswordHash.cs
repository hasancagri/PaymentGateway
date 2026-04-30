using System.Security.Cryptography;
using System.Text;
using Common.Shared;

namespace PaymentGatewayApi.Modules.IAM.Users.ValueObjects;

public sealed record PasswordHash
{
    public string Hash { get; }

    private PasswordHash(string hash) => Hash = hash;

    public static PasswordHash Create(string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 8)
            throw new DomainException("Password must be at least 8 characters.");

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(plainPassword)));

        return new PasswordHash(hash);
    }

    public static PasswordHash FromHash(string hash) => new(hash);

    public bool Verify(string plainPassword) =>
        Hash == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainPassword)));
}
