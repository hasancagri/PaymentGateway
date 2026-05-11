namespace IAM.Api.Domains.Users.Features.Queries;

public static class GetAllUsers
{
    public class GetAllUsersQuery { }

    public class UserListItem
    {
        public Guid Id { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public UserStatus Status { get; set; }
        public Guid? MerchantId { get; set; }
    }

    public class GetAllUsersHandler
    {
        public async Task<FeatureObjectResultModel<List<UserListItem>>> Handle(
            GetAllUsersQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var users = await session.Query<User>().ToListAsync(ct);

            return FeatureObjectResultModel<List<UserListItem>>.Ok(users.Select(x => new UserListItem
            {
                Id = x.Id,
                Email = x.Email.Value,
                FirstName = x.FullName.FirstName,
                LastName = x.FullName.LastName,
                Status = x.Status,
                MerchantId = x.MerchantId
            }).ToList());
        }
    }
}