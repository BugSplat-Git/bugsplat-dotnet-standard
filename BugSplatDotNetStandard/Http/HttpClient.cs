using System.Net.Http;

namespace BugSplatDotNetStandard.Http 
{
    internal interface IHttpClientFactory
    {
        HttpClient CreateClient();
    }

    internal class HttpClientFactory : IHttpClientFactory
    {
        public static IHttpClientFactory Default = new HttpClientFactory();

        public HttpClient CreateClient()
        {
            return new HttpClient();
        }
    }
}