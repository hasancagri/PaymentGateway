using PaymentGatewayApi.Modules.IAM.Roles.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.IAM.Roles.Features.Commands;

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
            IamContext db,
            CancellationToken ct)
        {
            var roleResult = Role.Create(cmd.Name, cmd.IsSystem);
            if (!roleResult.IsSuccess)
                return FeatureObjectResultModel<CreateRoleResponse>.Error(roleResult.Messages!);

            await db.Set<Role>().AddAsync(roleResult.Data!, ct);
            return FeatureObjectResultModel<CreateRoleResponse>.Ok(new CreateRoleResponse { Id = roleResult.Data!.Id });
        }
    }
}