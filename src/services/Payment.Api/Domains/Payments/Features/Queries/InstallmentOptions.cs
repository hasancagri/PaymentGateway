using System.Globalization;

namespace Payment.Api.Domains.Payments.Features.Queries;

/// <summary>
/// 033 US2: kart (BIN) + tutar için iyzico taksit seçeneklerini döner (installmentNumber + toplam
/// tutar, banka vade farkı dahil). Ödeme öncesi — çekimin PaidPrice'ı buradan seçilir.
/// </summary>
public static class InstallmentOptions
{
    public record InstallmentOptionsQuery(Guid MerchantId, string Bin, decimal Price);

    public record InstallmentOptionsBody(string Bin, decimal Price);

    public class InstallmentOptionItem
    {
        public int InstallmentNumber { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class InstallmentOptionsResponse
    {
        public List<InstallmentOptionItem> InstallmentDetails { get; set; } = new();
    }

    public class InstallmentOptionsQueryHandler
    {
        public async Task<FeatureObjectResultModel<InstallmentOptionsResponse>> Handle(
            InstallmentOptionsQuery query, ProviderOptions providerOptions, CancellationToken ct)
        {
            InstallmentInfo info;
            try
            {
                info = await InstallmentInfo.Retrieve(new RetrieveInstallmentInfoRequest
                {
                    Locale = "tr",
                    ConversationId = "inst-" + query.MerchantId.ToString("N")[..8],
                    BinNumber = query.Bin,
                    Price = query.Price.ToString(CultureInfo.InvariantCulture)
                }, providerOptions);
            }
            catch
            {
                return FeatureObjectResultModel<InstallmentOptionsResponse>.Error(new MessageItem
                { Property = nameof(query.Bin), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (info is null || info.Status != "success")
                return FeatureObjectResultModel<InstallmentOptionsResponse>.Error(new MessageItem
                { Property = nameof(query.Bin), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            var items = new List<InstallmentOptionItem>();
            foreach (var detail in info.InstallmentDetails ?? new())
            foreach (var price in detail.InstallmentPrices ?? new())
            {
                if (price.InstallmentNumber is { } n &&
                    decimal.TryParse(price.TotalPrice, NumberStyles.Number, CultureInfo.InvariantCulture, out var total))
                    items.Add(new InstallmentOptionItem { InstallmentNumber = n, TotalPrice = total });
            }

            return FeatureObjectResultModel<InstallmentOptionsResponse>.Ok(new InstallmentOptionsResponse
            {
                InstallmentDetails = items.OrderBy(i => i.InstallmentNumber).ToList()
            });
        }
    }
}

public static class InstallmentOptionsEndpoint
{
    public static RouteGroupBuilder InstallmentOptionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/installment-options",
                async (Guid merchantId, [FromBody] InstallmentOptions.InstallmentOptionsBody body, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<InstallmentOptions.InstallmentOptionsResponse>>(
                        new InstallmentOptions.InstallmentOptionsQuery(merchantId, body.Bin, body.Price));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("InstallmentOptions")
            .MapToApiVersion(1, 0)
            .RequireAuthorization(AuthorizationScopes.PaymentCharge, AuthorizationPolicies.MerchantScoped)
            .Produces<InstallmentOptions.InstallmentOptionsResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return group;
    }
}
