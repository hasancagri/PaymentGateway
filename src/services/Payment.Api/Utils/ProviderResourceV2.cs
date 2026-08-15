#nullable disable
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Payment.Api.Utils
{
    /// <summary>
    /// iyzico V2 yanıt tabanı: her yanıt tipi bundan türer (Status/Error alanları + korelasyon header'ı).
    /// Ayrıca isteğe V2 imza header'larını (HMAC-SHA256) üretir. Saf transport — domain bilmez.
    /// </summary>
    public class ProviderResourceV2
    {
        private const string AUTHORIZATION = "Authorization";
        private const string CONVERSATION_ID_HEADER_NAME = "x-conversation-id";
        private const string CLIENT_VERSION_HEADER_NAME = "x-iyzi-client-version";
        private const string IYZIWS_V2_HEADER_NAME = "IYZWSv2 ";

        public string Status { get; set; }
        public int StatusCode { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorGroup { get; set; }
        public string ConversationId { get; set; }
        public long SystemTime { get; set; }
        public string Locale { get; set; }

        public void AppendWithHttpResponseHeaders(HttpResponseMessage httpResponseMessage)
        {
            HttpHeaders responseHeaders = httpResponseMessage.Headers;
            this.StatusCode = Convert.ToInt32(httpResponseMessage.StatusCode);
            if (responseHeaders.TryGetValues(CONVERSATION_ID_HEADER_NAME, out var values))
            {
                string conversationId = values.First();
                this.ConversationId = !string.IsNullOrWhiteSpace(conversationId) ? conversationId : null;
            }
        }

        /// <summary>İstek gövdesi (JSON) üzerinden V2 imza + zorunlu header'ları üretir.</summary>
        public static Dictionary<string, string> GetHttpHeadersWithRequestBody(object request, string url, ProviderOptions options, string conversationId)
        {
            var headers = new Dictionary<string, string>
            {
                { "Accept", "application/json" },
                { CLIENT_VERSION_HEADER_NAME, ProviderConstants.CLIENT_VERSION },
                { CONVERSATION_ID_HEADER_NAME, conversationId },
                { AUTHORIZATION, PrepareAuthorizationString(request, url, options) }
            };
            return headers;
        }

        private static string PrepareAuthorizationString(object request, string url, ProviderOptions options)
        {
            string randomKey = DateTime.Now.ToString("ddMMyyyyhhmmssffff");
            string uriPath = FindUriPath(url);
            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            string payload = request != null ? uriPath + JsonConvert.SerializeObject(request, settings) : uriPath;
            string dataToEncrypt = randomKey + payload;
            string hash = HashGeneratorV2.GenerateHash(options.ApiKey, options.SecretKey, randomKey, dataToEncrypt);
            return IYZIWS_V2_HEADER_NAME + hash;
        }

        private static string FindUriPath(string url)
        {
            int startIndex = url.IndexOf(".com") + 4;
            int endIndex = url.IndexOf("?");
            int length = endIndex == -1 ? url.Length - startIndex : endIndex - startIndex;
            return url.Substring(startIndex, length);
        }
    }
}