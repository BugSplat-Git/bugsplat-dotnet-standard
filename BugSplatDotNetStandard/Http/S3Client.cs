using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace BugSplatDotNetStandard.Http
{
    internal interface IS3ClientFactory
    {
        IS3Client CreateClient();
    }

    internal class S3ClientFactory : IS3ClientFactory
    {
        public static IS3ClientFactory Default = new S3ClientFactory();

        public IS3Client CreateClient()
        {
            return new S3Client(HttpClientFactory.Default);
        }
    }

    interface IS3Client: IDisposable
    {
        Task<HttpResponseMessage> UploadFileBytesToPresignedURL(Uri uri, byte[] bytes);
        Task<HttpResponseMessage> UploadFileStreamToPresignedURL(Uri uri, Stream fileStream);
    }

    internal class S3Client: IS3Client
    {
        private HttpClient httpClient;

        public S3Client (IHttpClientFactory factory)
        {
            this.httpClient = factory.CreateClient();
            this.httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
        }
        public async Task<HttpResponseMessage> UploadFileBytesToPresignedURL(Uri uri, byte[] bytes)
        {
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            return await httpClient.PutAsync(uri, new ByteArrayContent(bytes));
        }

        public async Task<HttpResponseMessage> UploadFileStreamToPresignedURL(Uri uri, Stream fileStream)
        {
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
            return await httpClient.PutAsync(uri, new StreamContent(fileStream));
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }
    }
}