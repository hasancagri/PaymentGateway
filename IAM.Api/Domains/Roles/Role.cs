using IAM.Api.Domains.Roles.Entities;
using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;

namespace PaymentGatewayApi.Modules.IAM.Roles;

public sealed class Role : AggregateRoot
{
    public RoleName Name { get; init; }
    public bool IsSystem { get; init; }

    [Newtonsoft.Json.JsonProperty]
    private List<PagePermission> _permissions = [];
    public IReadOnlyCollection<PagePermission> Permissions => _permissions.AsReadOnly();

    private Role()
    {
    }

    public static ResultDomain<Role> Create(string name, bool isSystem = false)
    {
        var nameResult = RoleName.Create(name);
        if (!nameResult.IsSuccess) return ResultDomain<Role>.Error(nameResult.Messages!);

        return ResultDomain<Role>.Ok(new Role { Name = nameResult.Data!, IsSystem = isSystem });
    }

    public ResultDomain AddPermission(string pageRoute, string action)
    {
        var page = _permissions.FirstOrDefault(p => p.PageRoute == pageRoute);
        if (page is null)
        {
            page = PagePermission.Create(pageRoute);
            _permissions.Add(page);
        }

        return page.AddAction(action);
    }

    public ResultDomain RemovePermission(Guid permissionId)
    {
        var permission = _permissions.SingleOrDefault(p => p.Id == permissionId);
        if (permission is null)
            return ResultDomain.Error(new MessageItem { Code = "PagePermission.NotFound" });

        _permissions.Remove(permission);
        return ResultDomain.Ok();
    }
}