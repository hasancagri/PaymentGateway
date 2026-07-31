using System.Net.Http.Json;
using System.Text.Json;

namespace Admin.Clients;

/// <summary>Ortak yanıt okuma: 2xx → Data; hata → { isSuccess, messages } zarfı.</summary>
public abstract class ApiClientBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected HttpClient Http { get; }

    protected ApiClientBase(HttpClient http) => Http = http;

    /// <summary>HTTP çağrısını sarar: transport hatası (API kapalı/çözülemedi) → dostça sunucu hatası.</summary>
    protected async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        try
        {
            using var response = await send();
            return await ReadResultAsync<T>(response, ct);
        }
        catch
        {
            return ApiResult<T>.Fail(new List<ApiMessage> { new() { Code = "COMMON_MESSAGE_SERVER_ERROR" } });
        }
    }

    private async Task<ApiResult<T>> ReadResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<T>(Json, ct);
            return ApiResult<T>.Ok(data);
        }

        return ApiResult<T>.Fail(await ReadMessagesAsync(response, ct));
    }

    private static async Task<List<ApiMessage>> ReadMessagesAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<ErrorEnvelope>(Json, ct);
            if (envelope?.Messages is { Count: > 0 } messages)
                return messages;
        }
        catch
        {
            // gövde beklenen zarf değil → genel hata
        }

        return new List<ApiMessage> { new() { Code = "COMMON_MESSAGE_SERVER_ERROR" } };
    }
}