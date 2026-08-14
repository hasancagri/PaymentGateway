using System.Net.Http.Json;

namespace Admin.Clients;

public interface IRegisterRequestApiClient
{
    Task<ApiResult<RegisterRequestsResponse>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResult<ApproveRegisterResult>> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<ApiResult<RejectRegisterResult>> RejectAsync(Guid id, string reason, CancellationToken ct = default);
}

public class RegisterRequestApiClient : ApiClientBase, IRegisterRequestApiClient
{
    public RegisterRequestApiClient(HttpClient http) : base(http)
    {
    }

    public Task<ApiResult<RegisterRequestsResponse>> GetAllAsync(CancellationToken ct = default) =>
        SendAsync<RegisterRequestsResponse>(() => Http.GetAsync("/api/v1/register-requests", ct), ct);

    public Task<ApiResult<ApproveRegisterResult>> ApproveAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<ApproveRegisterResult>(() => Http.PostAsync($"/api/v1/register-requests/{id}/approve", null, ct), ct);

    public Task<ApiResult<RejectRegisterResult>> RejectAsync(Guid id, string reason, CancellationToken ct = default) =>
        SendAsync<RejectRegisterResult>(() =>
            Http.PostAsJsonAsync($"/api/v1/register-requests/{id}/reject", new { reason }, ct), ct);
}
