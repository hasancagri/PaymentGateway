namespace PaymentGatewayApi.Modules.IAM.Roles.Entities;

public sealed class PageAction : BaseModel
{
    public string Action { get; init; }
    public Guid PagePermissionId { get; init; }

    private PageAction()
    {
    }

    internal static PageAction Create(string action) => new()
    {
        Action = action.Trim()
    };
}