namespace MerchantManagement.Api.Auths;

public record UserSessionCache
{
    public Guid UserId { get; init; }
    public List<PageAccess> Pages { get; init; } = [];
}

public record PageAccess
{
    public required string Route { get; init; }
    public List<string> Actions { get; init; } = [];
}