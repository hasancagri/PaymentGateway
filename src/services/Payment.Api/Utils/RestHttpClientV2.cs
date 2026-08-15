#nullable disable
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Payment.Api.Utils;

/// <summary>
/// iyzico V2 HTTP istemcisi: isteği camelCase JSON gövdeyle gönderir, yanıtı <typeparamref name="T"/>'ye
/// deserialize eder. Yalnız POST/DELETE kullanılıyor (charge/tokenize/installment/revoke). Saf transport.
/// </summary>
public class RestHttpClientV2
{
    private static readonly System.Net.Http.HttpClient Client;

    static RestHttpClientV2()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Client = new System.Net.Http.HttpClient();
    }

    public static RestHttpClientV2 Create() => new RestHttpClientV2();

    public Task<T> PostAsync<T>(string url, Dictionary<string, string> headers, object request) where T : ProviderResourceV2
        => SendAsync<T>(HttpMethod.Post, url, headers, request);

    public Task<T> DeleteAsync<T>(string url, Dictionary<string, string> headers, object request) where T : ProviderResourceV2
        => SendAsync<T>(HttpMethod.Delete, url, headers, request);

    private static async Task<T> SendAsync<T>(HttpMethod method, string url, Dictionary<string, string> headers, object request) where T : ProviderResourceV2
    {
        var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
        var content = new StringContent(JsonConvert.SerializeObject(request, settings), Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage { Method = method, RequestUri = new Uri(url), Content = content };
        foreach (var header in headers)
            requestMessage.Headers.Add(header.Key, header.Value);

        var httpResponseMessage = await Client.SendAsync(requestMessage);
        var readAsString = await httpResponseMessage.Content.ReadAsStringAsync();
        var response = JsonConvert.DeserializeObject<T>(readAsString);
        response.AppendWithHttpResponseHeaders(httpResponseMessage);
        return response;
    }
}