namespace IAM.Api.Domains.Roles.Features.Commands;

public static class CreateRole
{
    public class CreateRoleCommand
    {
        public required string Name { get; set; }
        public bool IsSystem { get; set; } = false;
    }

    public class CreateRoleResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateRoleHandler
    {
        public async Task<FeatureObjectResultModel<CreateRoleResponse>> Handle(
            CreateRoleCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var roleResult = Role.Create(cmd.Name, cmd.IsSystem);
            if (!roleResult.IsSuccess)
                return FeatureObjectResultModel<CreateRoleResponse>.Error(roleResult.Messages!);

            session.Store(roleResult.Data!);
            return FeatureObjectResultModel<CreateRoleResponse>.Ok(new CreateRoleResponse { Id = roleResult.Data!.Id });
        }
    }
}