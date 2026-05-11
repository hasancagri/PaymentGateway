namespace IAM.Api.Domains.Users.ValueObjects;

public sealed record FullName
{
    public string FirstName { get; }
    public string LastName { get; }

    [Newtonsoft.Json.JsonConstructor]
    private FullName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    public static ResultDomain<FullName> Create(string firstName, string lastName)
    {
        var errors = new List<MessageItem>();
        if (string.IsNullOrWhiteSpace(firstName))
            errors.Add(new MessageItem { Code = "FullName.FirstNameEmpty" });
        if (string.IsNullOrWhiteSpace(lastName))
            errors.Add(new MessageItem { Code = "FullName.LastNameEmpty" });
        if (errors.Count > 0) return ResultDomain<FullName>.Error(errors);
        return ResultDomain<FullName>.Ok(new FullName(firstName.Trim(), lastName.Trim()));
    }

    public static FullName FromPersistence(string firstName, string lastName) => new(firstName, lastName);

    public string Display => $"{FirstName} {LastName}";
    public override string ToString() => Display;
}