namespace Common.Utils.Helpers;

public interface IJwtHelper : ISingletonDependency
{
    IAccessToken Create(IClaimInformation claimInformation);
}

public interface IAccessToken
{
    public string? Token { get; set; }

    public DateTime Expiration { get; set; }

    public string? RefreshToken { get; set; }
}

public interface IClaimInformation
{
    string FirstName { get; }
    string SurName { get; }
    string? Email { get; }
    string? Phone { get; }
}

public sealed class UserClaimInfo : IClaimInformation
{
    public string FirstName { get; }
    public string SurName { get; }
    public string? Email { get; }
    public string? Phone => null;

    public UserClaimInfo(string firstName, string surName, string email)
    {
        FirstName = firstName;
        SurName = surName;
        Email = email;
    }
}