using FluentValidation;
using Wolverine.Attributes;

namespace PaymentGatewayApi.Modules.IAM.Users.Features.Commands;

public static class NewCustomer
{
    [CacheResult("Customer")]
    public class NewCustomerCommand
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class NewCustomerResponse
    {
        public Guid Id { get; set; }
    }

    public class NewCustomerCommandValidator : AbstractValidator<NewCustomerCommand>
    {
        public NewCustomerCommandValidator()
        {
            RuleFor(x => x.Name).NotEmpty().Must(x => x.Length is >= 6 and <= 32)
                .WithErrorCode(CommonResourceConstants.COMMON_MESSAGE_VALUE_MIN_LENGHT_ERROR);

            RuleFor(x => x.Email).NotEmpty()
                .WithErrorCode(CommonResourceConstants.COMMON_MESSAGE_VALUE_MIN_LENGHT_ERROR);
        }
    }

    [Transactional]
    public class NewCustomerHandler
    {
        public async Task<FeatureObjectResultModel<NewCustomerResponse>> Handle(
            NewCustomerCommand cmd,
            VenueContext db,
            CancellationToken ct)
        {
            var name = CustomerName.FromPersistence(cmd.Name);
            var email = Email.FromPersistence(cmd.Email);

            var customerExists = await db.Set<Customer>()
                .AnyAsync(s => s.Name == name && s.Email == email, ct);

            if (customerExists)
                return FeatureObjectResultModel<NewCustomerResponse>.Error(new MessageItem
                {
                    Table = nameof(Customer),
                    Property = nameof(Customer.Name),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_DUPLICATE
                });

            var customerResult = Customer.Create(cmd.Name, cmd.Email);

            if (!customerResult.IsSuccess)
                return FeatureObjectResultModel<NewCustomerResponse>.Error(customerResult.Messages);

            await db.Set<Customer>().AddAsync(customerResult.Data!, ct);

            return FeatureObjectResultModel<NewCustomerResponse>.Ok(new NewCustomerResponse
            {
                Id = customerResult.Data!.Id
            });
        }
    }
}