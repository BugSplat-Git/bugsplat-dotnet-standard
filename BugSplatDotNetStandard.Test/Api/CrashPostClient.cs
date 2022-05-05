using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using static Tests.HttpContentVerifiers;
using static Tests.StackTraceFactory;

namespace Tests
{
    public class CrashPostClientTest
    {
        private string database;
        const string application = "my-net-crasher";
        const string version = "1.0";

        [OneTimeSetUp]
        public void Setup()
        {
            DotNetEnv.Env.Load();
            database = System.Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
        }


        [Test]
        public void CrashPostClient_Constructor_ShouldThrowIfHttpClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new CrashPostClient(null, S3ClientFactory.Default));
        }

        [Test]
        public void CrashPostClient_Constructor_ShouldThrowIfS3ClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new CrashPostClient(HttpClientFactory.Default, null));
        }

        [Test]
        public void CrashPostClient_PostException_ShouldCallPostAsyncWithUriAndMultipartFormDataContent()
        {
            var expectedUri = $"https://{database}.bugsplat.com/post/dotnetstandard/";
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            bugsplat.Description = "dangit bobby";
            bugsplat.Email = "bobby@bugsplat.com";
            bugsplat.IpAddress = "192.168.0.1";
            bugsplat.Key = "en-US";
            bugsplat.User = "@bobbyg603";
            var expectedFormDataParams = new List<string>() {
                "name=database", database,
                "name=appName", application,
                "name=appVersion", version,
                "name=description", bugsplat.Description,
                "name=email", bugsplat.Email,
                "name=appKey", bugsplat.Key,
                "name=user", bugsplat.User,
                "name=callstack", stackTrace,
                "name=crashTypeId", $"{(int)bugsplat.ExceptionType}"
            };
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            
            var sut = new CrashPostClient(httpClientFactory, S3ClientFactory.Default);

            var postResult = sut.PostException(
                database,
                application,
                version,
                stackTrace,
                bugsplat
            );

            mockHttp.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                        && req.RequestUri.ToString().Equals(expectedUri)
                            && ContainsValues(
                                req.Content.ReadAsStringAsync().Result,
                                expectedFormDataParams
                            )
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void CrashPostClient_PostException_ShouldCallPostAsyncWithUriAndMultipartFormDataContentOverrides()
        {
            var stackTrace = CreateStackTrace();
            var bugsplat = new BugSplat(database, application, version);
            var overrideOptions = new ExceptionPostOptions();
            overrideOptions.Description = "dangit bobby";
            overrideOptions.Email = "bobby@bugsplat.com";
            overrideOptions.IpAddress = "192.168.0.1";
            overrideOptions.Key = "en-US";
            overrideOptions.User = "@bobbyg603";
            overrideOptions.ExceptionType = BugSplat.ExceptionTypeId.Unity;
            var expectedFormDataParams = new List<string>() {
                "name=database", database,
                "name=appName", application,
                "name=appVersion", version,
                "name=description", overrideOptions.Description,
                "name=email", overrideOptions.Email,
                "name=appKey", overrideOptions.Key,
                "name=user", overrideOptions.User,
                "name=callstack", stackTrace,
                "name=crashTypeId", $"{(int)overrideOptions.ExceptionType}"
            };
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            
            var sut = new CrashPostClient(httpClientFactory, S3ClientFactory.Default);

            var postResult = sut.PostException(
                database,
                application,
                version,
                stackTrace,
                bugsplat,
                overrideOptions
            );

            mockHttp.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                        && ContainsValues(
                            req.Content.ReadAsStringAsync().Result,
                            expectedFormDataParams
                        )
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

       private Mock<HttpMessageHandler> CreateMockHttpClientForAuthenticateSuccess()
        {
            var response = new HttpResponseMessage();
            response.Content = new StringContent("");
            response.StatusCode = HttpStatusCode.OK;
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(response)
               .Verifiable();
 
            return handlerMock;
        }
    }
}