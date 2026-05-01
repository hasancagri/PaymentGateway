namespace Common.Auths;

public interface ICurrentUser : ISingletonDependency
{
    public Guid Id { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}