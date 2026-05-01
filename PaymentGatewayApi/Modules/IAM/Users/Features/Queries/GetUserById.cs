using PaymentGatewayApi.Modules.IAM.Users.Enums;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Queries;

public static class GetUserById
{
    public class GetUserByIdQuery
    {
        public required Guid UserId { get; set; }
    }

    public class GetUserByIdResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public UserStatus Status { get; set; }
        public Guid? MerchantId { get; set; }
        public List<Guid> RoleIds { get; set; } = [];
    }

    public class GetUserByIdHandler
    {
        public async Task<FeatureObjectResultModel<GetUserByIdResponse>> Handle(
            GetUserByIdQuery query,
            IamContext db,
            CancellationToken ct)
        {
            var user = await db.Set<User>()
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(x => x.Id == query.UserId, ct);

            if (user is null)
                return FeatureObjectResultModel<GetUserByIdResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            return FeatureObjectResultModel<GetUserByIdResponse>.Ok(new GetUserByIdResponse
            {
                Id = user.Id,
                Email = user.Email.Value,
                FirstName = user.FullName.FirstName,
                LastName = user.FullName.LastName,
                Status = user.Status,
                MerchantId = user.MerchantId,
                RoleIds = user.Roles.Select(r => r.RoleId).ToList()
            });
        }
    }
}