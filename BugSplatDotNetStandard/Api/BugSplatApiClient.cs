using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Http;
using static BugSplatDotNetStandard.Utils.ArgumentContracts;

namespace BugSplatDotNetStandard.Api
{
    public interface IBugSplatApiClient: IDisposable
    {
        /// <summary>
        /// Flag indicating if the client has been authenticated
        /// </summary>
        bool Authenticated { get; }

        /// <summary>
        /// The BugSplat API server location
        /// </summary>
        Uri Host { get; set; }

        /// <summary>
        /// Authenicate with the BugSplat backend and persist credentials for future requests
        /// </summary>
        Task<HttpResponseMessage> Authenticate();

        /// <summary>
        /// Make a POST request to a route relative to Host
        /// </summary>
        /// <param name="route">Relative URL that is relative to Host</param>
        /// <param name="content">POST content</param>
        Task<HttpResponseMessage> PostAsync(string route, HttpContent content);

        /// <summary>
        /// Make a GET request to a route relative to Host
        /// </summary>
        /// <param name="route">Relative URL that is relative to Host</param>
        Task<HttpResponseMessage> GetAsync(string route);
    }

    /// <summary>
    /// Used to make authenticated request to various BugSplat APIs via email and password authentication
    /// </summary>
    public class BugSplatApiClient : IBugSplatApiClient
    {
        public bool Authenticated { get; private set; }

        public Uri Host { get; set; } = new Uri("https://app.bugsplat.com");

        private string email;
        private string password;
        private HttpClient httpClient;

        internal BugSplatApiClient(
            string email,
            string password,
            IHttpClientFactory httpClientFactory
        )
        {
            ThrowIfArgumentIsNullOrEmpty(email, "email");
            ThrowIfArgumentIsNullOrEmpty(password, "password");
            ThrowIfArgumentIsNull(httpClientFactory, "httpClientFactory");

            this.email = email;
            this.password = password;
            this.httpClient = httpClientFactory.CreateClient();
        }

        /// <summary>
        /// Create an unauthenticated BugSplatApiClient that will use an email and password to authenticate
        /// </summary>
        /// <param name="email">BugSplat account email</param>
        /// <param name="password">BugSplat account password</param>
        public static BugSplatApiClient Create(string email, string password)
        {
            return new BugSplatApiClient(email, password, HttpClientFactory.Default);
        }

        /// <summary>
        /// Authenticate with the BugSplat API via email and password
        /// </summary>
        public async Task<HttpResponseMessage> Authenticate()
        {
            var url = new Uri(Host, "/api/authenticatev3");
            var formData = new MultipartFormDataContent()
            {
                { new StringContent(email), "email" },
                { new StringContent(password), "password" }
            };

            var authenticateResponse = await this.httpClient.PostAsync(url, formData);

            ThrowIfHttpRequestFailed(authenticateResponse);

            var setCookieHeader = authenticateResponse.Headers.FirstOrDefault(header => header.Key.Contains("Set-Cookie")).Value;
            var xsrfCookie = setCookieHeader.FirstOrDefault(value => value.Contains("xsrf-token"));
            var xsrfToken = xsrfCookie.Split(';').First().Split('=').Last();
            this.httpClient.DefaultRequestHeaders.Add("xsrf-token", xsrfToken);
            this.Authenticated = true;

            return authenticateResponse;
        }

        /// <summary>
        /// Make a GET request to the BugSplat API
        /// </summary>
        public async Task<HttpResponseMessage> GetAsync(string route)
        {
            var url = new Uri(Host, route);
            return await this.httpClient.GetAsync(url);
        }

        /// <summary>
        /// Make a POST request to the BugSplat API
        /// </summary>
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