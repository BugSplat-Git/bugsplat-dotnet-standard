using System;
using System.IO;
using System.Net;
using System.Net.Http;
using BugSplatDotNetStandard.Http;
using Moq;

namespace Tests
{
    internal class FakeS3ClientFactory: IS3ClientFactory
    {
        private IS3Client client;
        public FakeS3ClientFactory(IS3Client client)
        {
            this.client = client;
        }

        public IS3Client CreateClient()
        {
            return this.client;
        }

        public static IS3ClientFactory CreateMockS3ClientFactory()
        {
            var s3UploadFileStreamResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            };
            s3UploadFileStreamResponse.Headers.Add("ETag", "\"test\"");

            var mockS3Client = new Mock<IS3Client>();
            mockS3Client
                .Setup(s => s.UploadFileStreamToPresignedURL(It.IsAny<Uri>(), It.IsAny<Stream>()))
                .ReturnsAsync(s3UploadFileStreamResponse);
            
            return new FakeS3ClientFactory(mockS3Client.Object);
        }
    }
}