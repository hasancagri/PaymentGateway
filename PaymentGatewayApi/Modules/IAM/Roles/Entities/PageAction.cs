namespace PaymentGatewayApi.Modules.IAM.Roles.Entities;

public sealed class PageAction : BaseModel
{
    public string Action { get; private set; }
    public Guid PagePermissionId { get; private set; }

    private PageAction()
    {
    }

    internal static PageAction Create(string action) => new()
    {
        Action = action.Trim()
    };
}