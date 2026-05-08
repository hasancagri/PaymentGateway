using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway;

public class JwtService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opts = options.Value;

    public string GenerateToken(Guid merchantId, string merchantName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("merchant_id", merchantId.ToString()),
            new Claim("merchant_name", merchantName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}