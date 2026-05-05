namespace PaymentGatewayApi.Modules.IAM.Roles.Entities;

public sealed class PagePermission : BaseModel
{
    public string PageRoute { get; private set; }

    private readonly List<PageAction> _actions = [];
    public IReadOnlyCollection<PageAction> Actions => _actions.AsReadOnly();

    private PagePermission()
    {
    }

    internal static PagePermission Create(string pageRoute) => new()
    {
        PageRoute = pageRoute.Trim()
    };

    internal bool HasAction(string action) =>
        _actions.Any(a => a.Action == action);

    internal ResultDomain AddAction(string action)
    {
        if (HasAction(action))
            return ResultDomain.Error(new MessageItem { Code = "PagePermission.ActionAlreadyExists" });

        _actions.Add(PageAction.Create(action));
        return ResultDomain.Ok();
    }
}