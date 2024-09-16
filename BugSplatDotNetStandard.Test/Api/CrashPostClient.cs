using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using static Tests.StackTraceFactory;

namespace Tests
{
    [TestFixture]
    public class CrashPostClientTest
    {
        const string database = "fred";
        const string application = "my-net-crasher";
        const string version = "1.0";

        FileInfo lockedFile;
        FileInfo minidumpFile;
        FileStream lockedFileWriter;

        [OneTimeSetUp]
        public void SetUp()
        {
            var bytesToWrite = Encoding.UTF8.GetBytes("This file is locked");
            lockedFile = new FileInfo("lockedFile.txt");
            lockedFileWriter = File.Open(lockedFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
            lockedFileWriter.Write(bytesToWrite, 0, bytesToWrite.Length);
            lockedFileWriter.Flush();
            minidumpFile = new FileInfo("minidump.dmp");
            File.Create(minidumpFile.FullName).Close();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            lockedFileWriter.Close();
            lockedFile.Delete();
            minidumpFile.Delete();
        }

        [Test]
        public void CrashPostClient_Constructor_ShouldThrowIfHttpClientFactoryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new CrashPostClient(null, S3ClientFactory.Default));
        }

        [Test]
        public void CrashPostClient_Constructor_ShouldThrowIfS3ClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new CrashPostClient(HttpClientFactory.Default, null));
        }

        [Test]
        public async Task CrashPostClient_PostException_ShouldReturn200()
        {
            var expectedUri = $"https://{database}.bugsplat.com/api/getCrashUploadUrl?database={database}&appName={application}&appVersion={version}";
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            var postResult = await sut.PostException(
                database,
                application,
                version,
                stackTrace,
                ExceptionPostOptions.Create(bugsplat)
            );

            Assert.AreEqual(HttpStatusCode.OK, postResult.StatusCode);
        }

        [Test]
        public async Task CrashPostClient_PostMinidump_ShouldReturn200()
        {
            var expectedUri = $"https://{database}.bugsplat.com/api/getCrashUploadUrl?database={database}&appName={application}&appVersion={version}";
            var bugsplat = new BugSplat(database, application, version);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            var postResult = await sut.PostMinidump(
                database,
                application,
                version,
                new FileInfo("Files/minidump.dmp"),
                MinidumpPostOptions.Create(bugsplat)
            );

            Assert.AreEqual(HttpStatusCode.OK, postResult.StatusCode);
        }

        [Test]
        public void CrashPostClient_PostException_ShouldNotThrowIfAttachmentLocked()
        {
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Attachments.Add(lockedFile);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            Assert.DoesNotThrowAsync(async () =>
            {
                await sut.PostException(
                    database,
                    application,
                    version,
                    stackTrace,
                    ExceptionPostOptions.Create(bugsplat)
                );
            });
        }

        [Test]
        public void CrashPostClient_PostCrashFile_ShouldNotThrowIfAttachmentLocked()
        {
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Attachments.Add(lockedFile);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            Assert.DoesNotThrowAsync(async () =>
            {
                await sut.PostCrashFile(
                    database,
                    application,
                    version,
                    new FileInfo("minidump.dmp"),
                    MinidumpPostOptions.Create(bugsplat)
                );
            });
        }

        private Mock<HttpMessageHandler> CreateMockHttpClientForExceptionPost(string crashUploadUrl)
        {
            var getCrashUploadUrlResponse = new HttpResponseMessage();
            getCrashUploadUrlResponse.StatusCode = HttpStatusCode.OK;
            getCrashUploadUrlResponse.Content = new StringContent($"{{ \"url\": \"{crashUploadUrl}\" }}");

            var commitCrashUploadUrlReponse = new HttpResponseMessage();
            commitCrashUploadUrlReponse.StatusCode = HttpStatusCode.OK;

            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .SetupSequence<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(getCrashUploadUrlResponse)
               .ReturnsAsync(commitCrashUploadUrlReponse);

            return handlerMock;
        }
    }
}
