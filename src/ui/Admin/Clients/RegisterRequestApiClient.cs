using System.Net.Http.Json;

namespace Admin.Clients;

public interface IRegisterRequestApiClient
{
    Task<ApiResult<RegisterRequestsResponse>> GetPendingAsync(CancellationToken ct = default);
    Task<ApiResult<ApproveRegisterResult>> ApproveAsync(Guid id, string? note, CancellationToken ct = default);
    Task<ApiResult<IdResult>> RejectAsync(Guid id, string? note, CancellationToken ct = default);
}

public class RegisterRequestApiClient : ApiClientBase, IRegisterRequestApiClient
{
    public RegisterRequestApiClient(HttpClient http) : base(http)
    {
    }

    public Task<ApiResult<RegisterRequestsResponse>> GetPendingAsync(CancellationToken ct = default) =>
        SendAsync<RegisterRequestsResponse>(
            () => Http.GetAsync("/api/v1/register-requests?status=Pending", ct), ct);

    public Task<ApiResult<ApproveRegisterResult>> ApproveAsync(Guid id, string? note, CancellationToken ct = default) =>
        SendAsync<ApproveRegisterResult>(
            () => Http.PostAsJsonAsync($"/api/v1/register-requests/{id}/approve", new ReviewRequest(note), ct), ct);

    public Task<ApiResult<IdResult>> RejectAsync(Guid id, string? note, CancellationToken ct = default) =>
        SendAsync<IdResult>(
            () => Http.PostAsJsonAsync($"/api/v1/register-requests/{id}/reject", new ReviewRequest(note), ct), ct);
}
