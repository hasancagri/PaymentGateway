namespace Common.Auths;

public class CurrentUser : ICurrentUser
{
    public static ICurrentUser Load(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtSecurityToken = handler.ReadJwtToken(token.Replace("Bearer ", ""));
        var userId = jwtSecurityToken.Claims.Where(claim => claim.Type == ClaimTypes.NameIdentifier).Select(selector: v => v.Value).FirstOrDefault();
        var email = jwtSecurityToken.Claims.Where(claim => claim.Type == ClaimTypes.Email).Select(selector: v => v.Value).FirstOrDefault();
        var phone = jwtSecurityToken.Claims.Where(claim => claim.Type == ClaimTypes.MobilePhone).Select(selector: v => v.Value).FirstOrDefault();
        var name = jwtSecurityToken.Claims.Where(claim => claim.Type == ClaimTypes.Name).Select(selector: v => v.Value).FirstOrDefault();

        CurrentUser currentUser = new()
        {
            Id = new Guid(userId!),
            Email = email,
            Phone = phone,
            Name = name,
        };

        return currentUser;
    }
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
