namespace IAM.Api.Domains.Users.Features.Commands;

public static class Login
{
    public class LoginCommand
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    public class LoginCommandResponse
    {
        public string? Token { get; set; }
    }

    [Transactional]
    public class LoginHandler
    {
        public async Task<FeatureObjectResultModel<LoginCommandResponse>> Handle(
            LoginCommand cmd,
            IDocumentSession session,
            IJwtHelper jwtHelper,
            CancellationToken ct)
        {
            var user = await session.Query<User>().FirstOrDefaultAsync(x => x.Email.Value == cmd.Email, ct);

            if (user is null || !user.Login(cmd.Password))
            {
                return FeatureObjectResultModel<LoginCommandResponse>.Error(new MessageItem
                {
                    Table = nameof(User),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });
            }

            session.Store(user);

            var claimInfo = new UserClaimInfo(user.FullName.FirstName, user.FullName.LastName, user.Email.Value);
            var accessToken = jwtHelper.Create(claimInfo);

            return FeatureObjectResultModel<LoginCommandResponse>.Ok(new LoginCommandResponse
            {
                Token = accessToken.Token
            });
        }
    }
}