using PaymentGatewayBff.Models.Merchant;

namespace PaymentGatewayBff.Endpoints.Web;

public static class WebMerchantEndpoints
{
    private record ReasonRequest(string Reason);

    private record CreateMerchantRequest(
        string Name,
        string Email,
        string Phone,
        string Country,
        string City,
        string Mcc);

    private record UpdateMerchantRequest(
        Guid Id,
        string Name,
        string Email,
        string Phone,
        string Country,
        string City,
        string Mcc);

    public static IEndpointRouteBuilder MapWebMerchantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/merchants", async (MerchantApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetAllAsync(ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<List<WebMerchantListItem>>>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapGet("/merchants/{id:guid}", async (Guid id, MerchantApiClient client, CancellationToken ct) =>
        {
            var response = await client.GetByIdAsync(id, ct);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<WebMerchantDetail>>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPost("/merchants", async (CreateMerchantRequest req, MerchantApiClient client, CancellationToken ct) =>
        {
            var response = await client.CreateAsync(req, ct);
            var result = await response.Content.ReadFromJsonAsync<object>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
        });

        app.MapPut("/merchants/{id:guid}",
            async (Guid id, UpdateMerchantRequest req, MerchantApiClient client, CancellationToken ct) =>
            {
                var response = await client.UpdateAsync(id, req, ct);
                var result = await response.Content.ReadFromJsonAsync<object>(ct);
                return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
            });

        app.MapPost("/merchants/{id:guid}/activate",
            async (Guid id, ReasonRequest req, MerchantApiClient client, CancellationToken ct) =>
            {
                var response = await client.ActivateAsync(id, req, ct);
                var result = await response.Content.ReadFromJsonAsync<object>(ct);
                return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
            });

        app.MapPost("/merchants/{id:guid}/deactivate",
            async (Guid id, ReasonRequest req, MerchantApiClient client, CancellationToken ct) =>
            {
                var response = await client.DeactivateAsync(id, req, ct);
                var result = await response.Content.ReadFromJsonAsync<object>(ct);
                return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
            });

        app.MapPost("/merchants/{id:guid}/suspend",
            async (Guid id, ReasonRequest req, MerchantApiClient client, CancellationToken ct) =>
            {
                var response = await client.SuspendAsync(id, req, ct);
                var result = await response.Content.ReadFromJsonAsync<object>(ct);
                return response.IsSuccessStatusCode ? Results.Ok(result) : Results.BadRequest(result);
            });

        return app;
    }
}