using System.Net.Http;
using BugSplatDotNetStandard.Http;

namespace Tests
{
    public class FakeHttpClientFactory: IHttpClientFactory
    {
        private HttpClient client;
        public FakeHttpClientFactory(HttpClient client)
        {
            this.client = client;
        }

        public HttpClient CreateClient()
        {
            return this.client;
        }
    }
}