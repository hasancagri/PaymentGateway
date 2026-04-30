namespace PaymentGatewayApi.Modules.IAM.Users.ValueObjects;

public sealed record FullName
{
    public string FirstName { get; }
    public string LastName  { get; }

    public FullName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new DomainException("First name cannot be empty.");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new DomainException("Last name cannot be empty.");

        FirstName = firstName.Trim();
        LastName  = lastName.Trim();
    }

    public string Display => $"{FirstName} {LastName}";
    public override string ToString() => Display;
}
