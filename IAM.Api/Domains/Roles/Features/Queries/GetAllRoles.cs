using IAM.Api.Domains.Roles;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Queries;

public static class GetAllRoles
{
    public class GetAllRolesQuery { }

    public class RoleListItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsSystem { get; set; }
    }

    public class GetAllRolesHandler
    {
        public async Task<FeatureObjectResultModel<List<RoleListItem>>> Handle(
            GetAllRolesQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var roles = await session.Query<Role>().ToListAsync(ct);

            return FeatureObjectResultModel<List<RoleListItem>>.Ok(roles.Select(x => new RoleListItem
            {
                Id = x.Id,
                Name = x.Name.Value,
                IsSystem = x.IsSystem
            }).ToList());
        }
    }
}