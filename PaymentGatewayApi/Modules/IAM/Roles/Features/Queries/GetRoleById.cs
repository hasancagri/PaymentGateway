namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Queries;

public static class GetRoleById
{
    public class GetRoleByIdQuery
    {
        public required Guid RoleId { get; set; }
    }

    public class GetRoleByIdResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsSystem { get; set; }
        public List<PagePermissionItem> Permissions { get; set; } = [];
    }

    public class PagePermissionItem
    {
        public Guid Id { get; set; }
        public string PageRoute { get; set; }
        public List<string> Actions { get; set; } = [];
    }

    public class GetRoleByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetRoleByIdResponse>> Handle(
            GetRoleByIdQuery query,
            IamContext db,
            CancellationToken ct)
        {
            var role = await db.Set<Role>()
                .Include(x => x.Permissions)
                .ThenInclude(x => x.Actions)
                .FirstOrDefaultAsync(x => x.Id == query.RoleId, ct);

            if (role is null)
                return FeatureObjectResultModel<GetRoleByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(Role),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetRoleByIdResponse>.Ok(new GetRoleByIdResponse
            {
                Id = role.Id,
                Name = role.Name.Value,
                IsSystem = role.IsSystem,
                Permissions = role.Permissions.Select(p => new PagePermissionItem
                {
                    Id = p.Id,
                    PageRoute = p.PageRoute,
                    Actions = p.Actions.Select(a => a.Action).ToList()
                }).ToList()
            });
        }
    }
}