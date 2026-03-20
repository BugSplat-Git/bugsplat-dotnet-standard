using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BugSplatDotNetStandard;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Tests.StackTraceFactory;
using System.Net.Http;
using System.Net;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BugSplatDotNetStandard.Utils;

namespace Tests
{
    [TestFixture]
    public class CrashPostClientTest
    {
        FileInfo lockedFile;
        FileInfo minidumpFile;
        FileStream lockedFileWriter;

        string database;
        string application = "TestApp";
        string version = "1.0.0";
        string clientId;
        string clientSecret;

        [OneTimeSetUp]
        public void SetUp()
        {
            DotNetEnv.Env.Load();
            database = Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
            clientId = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
            clientSecret = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");

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
        [Explicit]
        public void CrashPostClient_PostMinidump_Returns400IfFileIsTooBig()
        {
            var bugsplat = new BugSplat(database, application, version);
            var sut = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default);
            var largeFile = CreateLargeTempFile(1000 * 1024 * 1024); // 1 GB

            Assert.ThrowsAsync<Exception>(async () =>
            {
                await sut.PostMinidump(
                    database,
                    application,
                    version,
                    largeFile,
                    MinidumpPostOptions.Create(bugsplat)
                );
            }, "Failed to parse upload url: crash post size limit exceeded");
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

        [Test]
        [Explicit]
        public async Task CrashPostClient_PostCrashFile_PostCrashAndMetadata()
        {
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Description = "Test description";
            bugsplat.Email = "test@test.com";
            bugsplat.Key = "Test key";
            bugsplat.Notes = "Test notes";
            bugsplat.User = "Test user";
            bugsplat.Attributes.Add("key", "value");
            var oauth2ApiClient = OAuth2ApiClient.Create(clientId, clientSecret)
                .Authenticate()
                .Result;
            var crashDetailsClient = CrashDetailsClient.Create(oauth2ApiClient);
            var sut = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default);

            var postResult = await sut.PostException(
                database,
                application,
                version,
                stackTrace,
                ExceptionPostOptions.Create(bugsplat)
            );

            var postResponseContent = JObject.Parse(postResult.Content.ReadAsStringAsync().Result);
            var id = postResponseContent["crashId"].Value<int>();
            var crashDetails = await crashDetailsClient.GetCrashDetails(database, id);
            var crashDetailsContent = JObject.Parse(crashDetails.Content.ReadAsStringAsync().Result);
            Assert.AreEqual(HttpStatusCode.OK, postResult.StatusCode);
            Assert.AreEqual(bugsplat.Description, crashDetailsContent["description"].Value<string>());
            Assert.AreEqual(bugsplat.Email, crashDetailsContent["email"].Value<string>());
            Assert.AreEqual(bugsplat.Key, crashDetailsContent["appKey"].Value<string>());
            Assert.AreEqual(bugsplat.Notes, crashDetailsContent["comments"].Value<string>());
            Assert.AreEqual(bugsplat.User, crashDetailsContent["user"].Value<string>());
            Assert.AreEqual(JsonSerializer.Serialize(bugsplat.Attributes), crashDetailsContent["attributes"].Value<string>().Replace(" ", string.Empty));
        }

        [Test]
        [Explicit]
        public async Task CrashPostClient_PostCrashFile_AllowAdditionalFormDataAttachments()
        {
            var bugsplat = new BugSplat(database, application, version);
            var minidump = new FileInfo("Files/minidump.dmp");
            var oauth2ApiClient = OAuth2ApiClient.Create(clientId, clientSecret)
                .Authenticate()
                .Result;
            var crashDetailsClient = CrashDetailsClient.Create(oauth2ApiClient);
            var sut = new CrashPostClient(HttpClientFactory.Default, S3ClientFactory.Default);
            var fileName = "hello.txt";
            var expectedContent = "hello world!";
            var overrideOptions = new MinidumpPostOptions()
            {
                FormDataParams = new List<IFormDataParam>()
                {
                    new FormDataParam()
                    {
                        Content = new StringContent(expectedContent),
                        FileName = fileName,
                        Name = "file0"
                    }
                }
            };

            var postResult = await sut.PostMinidump(
                database,
                application,
                version,
                minidump,
                MinidumpPostOptions.Create(bugsplat),
                overrideOptions
            );

            var postResponseContent = JObject.Parse(postResult.Content.ReadAsStringAsync().Result);
            var id = postResponseContent["crashId"].Value<int>();
            var crashDetails = await crashDetailsClient.GetCrashDetails(database, id);
            var crashDetailsContent = JObject.Parse(crashDetails.Content.ReadAsStringAsync().Result);
            var crashZipUrl = crashDetailsContent["dumpfile"];
            using var client = new HttpClient();
            var crashZipResponse = await client.GetStreamAsync(crashZipUrl.ToString());
            var zipArchive = new ZipArchive(crashZipResponse);
            var attachment = zipArchive.GetEntry(fileName);
            using var reader = new StreamReader(attachment.Open());
            var attachmentContent = await reader.ReadToEndAsync();
            Assert.AreEqual(expectedContent, attachmentContent);
        }

        [Test]
        public async Task CrashPostClient_PostFeedback_ShouldReturn200()
        {
            var bugsplat = new BugSplat(database, application, version);
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            var postResult = await sut.PostFeedback(
                database,
                application,
                version,
                "Test feedback title",
                FeedbackPostOptions.Create(bugsplat)
            );

            Assert.AreEqual(HttpStatusCode.OK, postResult.StatusCode);
        }

        [Test]
        public async Task CrashPostClient_PostFeedback_ShouldUseCrashTypeId36()
        {
            var bugsplat = new BugSplat(database, application, version);
            var getCrashUrl = "https://fake.url.com";
            HttpRequestMessage capturedCommitRequest = null;
            var mockHttp = CreateMockHttpClientWithCapture(getCrashUrl, req => capturedCommitRequest = req);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);

            await sut.PostFeedback(
                database,
                application,
                version,
                "Test feedback title",
                FeedbackPostOptions.Create(bugsplat)
            );

            Assert.IsNotNull(capturedCommitRequest);
            var content = capturedCommitRequest.Content as MultipartFormDataContent;
            Assert.IsNotNull(content);
            var formData = await content.ReadAsStringAsync();
            Assert.That(formData, Does.Contain("36"));
        }

        [Test]
        public async Task CrashPostClient_PostFeedback_ShouldConstructFeedbackJsonWithEscaping()
        {
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Description = "Line1\nLine2\twith \"quotes\"";
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            byte[] capturedBytes = null;
            var mockTempFileFactory = new Mock<ITempFileFactory>();
            var mockTempFile = new Mock<ITempFile>();
            mockTempFile.Setup(t => t.File).Returns(new FileInfo("Files/minidump.dmp"));
            mockTempFileFactory
                .Setup(f => f.CreateFromBytes("feedback.json", It.IsAny<byte[]>()))
                .Callback<string, byte[]>((name, bytes) => capturedBytes = bytes)
                .Returns(mockTempFile.Object);
            mockTempFileFactory
                .Setup(f => f.CreateTempZip(It.IsAny<IEnumerable<FileInfo>>()))
                .Returns(mockTempFile.Object);
            mockTempFile
                .Setup(t => t.CreateFileStream(It.IsAny<FileMode>(), It.IsAny<FileAccess>()))
                .Returns(new MemoryStream(new byte[] { 0 }));

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);
            sut.TempFileFactory = mockTempFileFactory.Object;

            await sut.PostFeedback(
                database,
                application,
                version,
                "Title with \"quotes\"",
                FeedbackPostOptions.Create(bugsplat)
            );

            Assert.IsNotNull(capturedBytes);
            var feedbackJson = Encoding.UTF8.GetString(capturedBytes);
            Assert.That(feedbackJson, Does.Contain("Title with \\\"quotes\\\""));
            Assert.That(feedbackJson, Does.Contain("Line1\\nLine2\\twith \\\"quotes\\\""));
        }

        [Test]
        public async Task CrashPostClient_PostFeedback_ShouldUseOverrideDescription()
        {
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Description = "Default description";
            var getCrashUrl = "https://fake.url.com";
            var mockHttp = CreateMockHttpClientForExceptionPost(getCrashUrl);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();

            byte[] capturedBytes = null;
            var mockTempFileFactory = new Mock<ITempFileFactory>();
            var mockTempFile = new Mock<ITempFile>();
            mockTempFile.Setup(t => t.File).Returns(new FileInfo("Files/minidump.dmp"));
            mockTempFileFactory
                .Setup(f => f.CreateFromBytes("feedback.json", It.IsAny<byte[]>()))
                .Callback<string, byte[]>((name, bytes) => capturedBytes = bytes)
                .Returns(mockTempFile.Object);
            mockTempFileFactory
                .Setup(f => f.CreateTempZip(It.IsAny<IEnumerable<FileInfo>>()))
                .Returns(mockTempFile.Object);
            mockTempFile
                .Setup(t => t.CreateFileStream(It.IsAny<FileMode>(), It.IsAny<FileAccess>()))
                .Returns(new MemoryStream(new byte[] { 0 }));

            var sut = new CrashPostClient(httpClientFactory, mockS3ClientFactory);
            sut.TempFileFactory = mockTempFileFactory.Object;

            var overrideOptions = new FeedbackPostOptions
            {
                Description = "Override description"
            };

            await sut.PostFeedback(
                database,
                application,
                version,
                "Test title",
                FeedbackPostOptions.Create(bugsplat),
                overrideOptions
            );

            Assert.IsNotNull(capturedBytes);
            var feedbackJson = Encoding.UTF8.GetString(capturedBytes);
            Assert.That(feedbackJson, Does.Contain("Override description"));
            Assert.That(feedbackJson, Does.Not.Contain("Default description"));
        }

        private Mock<HttpMessageHandler> CreateMockHttpClientWithCapture(string crashUploadUrl, Action<HttpRequestMessage> captureCallback)
        {
            var getCrashUploadUrlResponse = new HttpResponseMessage();
            getCrashUploadUrlResponse.StatusCode = HttpStatusCode.OK;
            getCrashUploadUrlResponse.Content = new StringContent($"{{ \"url\": \"{crashUploadUrl}\" }}");

            var commitCrashUploadUrlReponse = new HttpResponseMessage();
            commitCrashUploadUrlReponse.StatusCode = HttpStatusCode.OK;

            var callCount = 0;
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync((HttpRequestMessage request, CancellationToken token) =>
               {
                   callCount++;
                   if (callCount == 1)
                       return getCrashUploadUrlResponse;
                   captureCallback(request);
                   return commitCrashUploadUrlReponse;
               });

            return handlerMock;
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

        private static FileInfo CreateLargeTempFile(long sizeInBytes, string fileNamePrefix = "LargeTestFile")
        {
            if (sizeInBytes < 0)
        {
            throw new ArgumentException("Size cannot be negative", nameof(sizeInBytes));
        }

        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{fileNamePrefix}_{Guid.NewGuid()}.tmp");
        const int bufferSize = 81920;
        byte[] buffer = new byte[bufferSize];

        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
        {
            fileStream.SetLength(sizeInBytes);

            long bytesWritten = 0;
            using (var rng = RandomNumberGenerator.Create())
            {
                while (bytesWritten < sizeInBytes)
                {
                    int bytesToWrite = (int)Math.Min(bufferSize, sizeInBytes - bytesWritten);
                    rng.GetBytes(buffer, 0, bytesToWrite);
                    fileStream.Write(buffer, 0, bytesToWrite);
                    bytesWritten += bytesToWrite;
                }
            }
        }

        return new FileInfo(tempFilePath);
        }
    }
}
