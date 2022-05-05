using System;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Http;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard.Api
{
    /// <summary>
    /// Used to make authenticated request to various BugSplat APIs via OAuth2 Client Credentials
    /// </summary>
    public class OAuth2ApiClient : IBugSplatApiClient
    {
        public bool Authenticated { get; private set; }

        public Uri Host { get; set; } = new Uri("https://app.bugsplat.com");

        private string clientId;
        private string clientSecret;
        private HttpClient httpClient;

        internal OAuth2ApiClient(
            string clientId,
            string clientSecret,
            IHttpClientFactory httpClientFactory
        )
        {
            ThrowIfArgumentIsNullOrEmpty(clientId, "clientId");
            ThrowIfArgumentIsNullOrEmpty(clientSecret, "clientSecret");
            ThrowIfArgumentIsNull(httpClientFactory, "httpClientFactory");

            this.clientId = clientId;
            this.clientSecret = clientSecret;
            this.httpClient = httpClientFactory.CreateClient();
        }

        /// <summary>
        /// Create an unauthenticated BugSplatApiClient that will use OAuth2 Client Credentials to authenticate
        /// </summary>
        /// <param name="clientId">BugSplat OAuth2 ClientId</param>
        /// <param name="clientSecret">BugSplat OAuth2 ClientId</param>
        public static OAuth2ApiClient Create(string clientId, string clientSecret)
        {
            return new OAuth2ApiClient(clientId, clientSecret, HttpClientFactory.Default);
        }

        /// <summary>
        /// Authenticate with the BugSplat API via OAuth2 Client Credentials
        /// </summary>
        public async Task<HttpResponseMessage> Authenticate()
        {
            var url = new Uri(Host, "/oauth2/authorize");
            var formData = new MultipartFormDataContent()
            {
                { new StringContent(clientId), "client_id" },
                { new StringContent(clientSecret), "client_secret" },
                { new StringContent("restricted"), "scope" },
                { new StringContent("client_credentials"), "grant_type" }
            };

            var authorizeResponse = await this.httpClient.PostAsync(url, formData);

            ThrowIfHttpRequestFailed(authorizeResponse);

            var json = await authorizeResponse.Content.ReadAsStringAsync();
            var jsonObj = new JsonObject(json);
            var tokenType = jsonObj.GetValue("token_type");
            var accessToken = jsonObj.GetValue("access_token");
            var authorizeHeader = $"{tokenType} {accessToken}";
            this.httpClient.DefaultRequestHeaders.Add("Authorization", authorizeHeader);
            this.Authenticated = true;

            return authorizeResponse;
        }

        public async Task<HttpResponseMessage> GetAsync(string route)
        {
            var url = new Uri(Host, route);
            return await this.httpClient.GetAsync(url);
        }

        public async Task<HttpResponseMessage> PostAsync(string route, HttpContent content)
        {
            var url = new Uri(Host, route);
            return await this.httpClient.PostAsync(url, content);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}