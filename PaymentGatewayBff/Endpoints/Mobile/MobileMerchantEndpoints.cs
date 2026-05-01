using PaymentGatewayBff.Clients;
using PaymentGatewayBff.Infrastructure;
using PaymentGatewayBff.Models.Merchant;

namespace PaymentGatewayBff.Endpoints.Mobile;

public static class MobileMerchantEndpoints
{
    public static IEndpointRouteBuilder MapMobileMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/merchants", async (MerchantApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetAllAsync(ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<WebMerchantListItem>>>(ct);
            if (!response.IsSuccessStatusCode || result?.Data is null)
                return Results.BadRequest(result);

            var mobile = result.Data.Select(m => new MobileMerchantListItem
            {
                Id = m.Id,
                Name = m.Name,
                Status = m.Status
            }).ToList();

            return Results.Ok(mobile);
        });

        app.MapGet("/merchants/{id:guid}", async (Guid id, MerchantApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetByIdAsync(id, ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<WebMerchantDetail>>(ct);
            if (!response.IsSuccessStatusCode || result?.Data is null)
                return Results.BadRequest(result);

            var mobile = new MobileMerchantDetail
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                Status = result.Data.Status,
                Email = result.Data.Email,
                Country = result.Data.Country
            };

            return Results.Ok(mobile);
        });

        return app;
    }
}