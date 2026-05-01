using PaymentGatewayApi.Modules.IAM.Users.ValueObjects;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class CreateUser
{
    public class CreateUserCommand
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public Guid? MerchantId { get; set; }
    }

    public class CreateUserResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateUserHandler
    {
        public async Task<FeatureObjectResultModel<CreateUserResponse>> Handle(
            CreateUserCommand cmd,
            IamContext db,
            CancellationToken ct)
        {
            var userResult = User.Create(cmd.Email, cmd.Password, cmd.FirstName, cmd.LastName, cmd.MerchantId);
            if (!userResult.IsSuccess)
            {
                return FeatureObjectResultModel<CreateUserResponse>.Error(userResult.Messages!);
            }
            
            await db.Set<User>().AddAsync(userResult.Data!, ct);
            return FeatureObjectResultModel<CreateUserResponse>.Ok(new CreateUserResponse
            {
                Id = userResult.Data!.Id
            });
        }
    }
}