using Microsoft.AspNetCore.Mvc;
using PaymentGatewayBff.Clients;
using PaymentGatewayBff.Infrastructure;
using PaymentGatewayBff.Models.Commission;

namespace PaymentGatewayBff.Endpoints.Web;

public static class WebCommissionEndpoints
{
    private record DefineMerchantCommissionRequest(Guid MerchantId, Guid BankCommissionId, int CardBrand, int CardType, int TransactionRegion, decimal MerchantRate);
    private record UpdateCommissionRateRequest(Guid CommissionId, decimal NewRate);

    public static IEndpointRouteBuilder MapWebCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/merchant-commissions", async ([AsParameters] Guid merchantId, [FromQuery] int page, CommissionApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetMerchantCommissionsAsync(merchantId, page, pageSize: 10, ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<WebMerchantCommissionListItem>>>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapGet("/merchant-commissions/{id:guid}", async (Guid id, CommissionApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetMerchantCommissionByIdAsync(id, ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<WebMerchantCommissionListItem>>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPost("/merchant-commissions", async (DefineMerchantCommissionRequest req, CommissionApiClient client, CancellationToken ct) =>
        {
            var response = await client.DefineMerchantCommissionAsync(req, ct);
            var result = await response.Content.ReadFromJsonAsync<object>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPatch("/merchant-commissions/{id:guid}/rate", async (Guid id, UpdateCommissionRateRequest req, CommissionApiClient client, CancellationToken ct) =>
        {
            var response = await client.UpdateMerchantCommissionRateAsync(id, req, ct);
            var result = await response.Content.ReadFromJsonAsync<object>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapGet("/bank-commissions", async (Guid? bankId, CommissionApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetBankCommissionsAsync(bankId, ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<WebBankCommissionListItem>>>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        return app;
    }
}