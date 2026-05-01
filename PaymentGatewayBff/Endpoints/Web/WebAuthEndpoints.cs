using PaymentGatewayBff.Clients;

namespace PaymentGatewayBff.Endpoints.Web;

public static class WebAuthEndpoints
{
    public static IEndpointRouteBuilder MapWebAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (HttpContext ctx, AuthApiClient client, CancellationToken ct) =>
        {
            var body = await ctx.Request.ReadFromJsonAsync<object>(ct);
            var response = await client.LoginAsync(body!, ct);
            var content = await response.Content.ReadFromJsonAsync<object>(ct);
            return response.IsSuccessStatusCode ? Results.Ok(content) : Results.BadRequest(content);
        });

        return app;
    }
}