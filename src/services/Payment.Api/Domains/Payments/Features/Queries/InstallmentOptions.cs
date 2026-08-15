using System.Globalization;
using Iyz = Payment.Api.Utils;
using Payment.Api.Options;

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

    /// <summary>iyzico "taksit sorgu" istek gövdesi (wire) — bu slice'a ait. camelCase JSON, base tip yok.</summary>
    public class RetrieveInstallmentInfoRequest
    {
        public string Locale { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public string BinNumber { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
    }

    /// <summary>iyzico taksit yanıtı (wire) — Status/Error alanları Iyz.ProviderResourceV2'den.</summary>
    public class InstallmentInfoResult : Iyz.ProviderResourceV2
    {
        public List<InstallmentDetail> InstallmentDetails { get; set; } = new();
    }

    public class InstallmentDetail
    {
        public List<InstallmentPrice> InstallmentPrices { get; set; } = new();
    }

    public class InstallmentPrice
    {
        public int? InstallmentNumber { get; set; }
        public string TotalPrice { get; set; } = string.Empty;
    }

    public class InstallmentOptionsQueryHandler
    {
        public async Task<FeatureObjectResultModel<InstallmentOptionsResponse>> Handle(
            InstallmentOptionsQuery query, Iyz.ProviderOptions providerOptions,
            IyzicoRequestOptions requestOptions, CancellationToken ct)
        {
            InstallmentInfoResult info;
            try
            {
                var request = new RetrieveInstallmentInfoRequest
                {
                    Locale = requestOptions.Locale,
                    ConversationId = requestOptions.ConversationId,
                    BinNumber = query.Bin,
                    Price = query.Price.ToString(CultureInfo.InvariantCulture)
                };
                var uri = providerOptions.BaseUrl + requestOptions.InstallmentPath;
                var headers = Iyz.ProviderResourceV2.GetHttpHeadersWithRequestBody(request, uri, providerOptions, request.ConversationId);
                info = await Iyz.RestHttpClientV2.Create().PostAsync<InstallmentInfoResult>(uri, headers, request);
            }
            catch
            {
                return FeatureObjectResultModel<InstallmentOptionsResponse>.Error(new MessageItem
                { Property = nameof(query.Bin), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
            }

            if (info is null || info.Status != requestOptions.SuccessStatus)
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