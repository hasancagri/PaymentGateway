namespace Common.Auths;

public class CurrentUser : ICurrentUser
{
    public static ICurrentUser Load(ClaimsPrincipal principal) => new CurrentUser
    {
        Id    = Guid.Parse(principal.FindFirst("sub")!.Value),
        Email = principal.FindFirst("email")?.Value,
        Name  = principal.FindFirst("given_name")?.Value + " "
              + principal.FindFirst("family_name")?.Value,
    };

    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}