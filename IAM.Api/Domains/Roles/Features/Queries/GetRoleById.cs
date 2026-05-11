namespace IAM.Api.Domains.Roles.Features.Queries;

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
    }

    public class GetRoleByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetRoleByIdResponse>> Handle(
            GetRoleByIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var role = await session.LoadAsync<Role>(query.RoleId, ct);

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
                IsSystem = role.IsSystem
            });
        }
    }
}