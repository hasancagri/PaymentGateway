using Microsoft.AspNetCore.Mvc;
using PaymentGatewayBff.Clients;
using PaymentGatewayBff.Infrastructure;
using PaymentGatewayBff.Models.Commission;

namespace PaymentGatewayBff.Endpoints.Mobile;

public static class MobileCommissionEndpoints
{
    public static IEndpointRouteBuilder MapMobileCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/merchant-commissions", async ([AsParameters] Guid merchantId, [FromQuery] int page, CommissionApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetMerchantCommissionsAsync(merchantId, page, pageSize: 5, ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<WebMerchantCommissionListItem>>>(ct);
            if (!response.IsSuccessStatusCode || result?.Data is null)
                return Results.BadRequest(result);

            var mobile = result.Data.Select(c => new MobileMerchantCommissionListItem
            {
                Id = c.Id,
                Rate = c.Rate,
                CardBrand = c.CardBrand
            }).ToList();

            return Results.Ok(mobile);
        });

        return app;
    }
}