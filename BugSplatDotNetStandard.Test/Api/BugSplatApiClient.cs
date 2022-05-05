using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using static Tests.HttpContentVerifiers;

namespace Tests
{
    public class BugSplatApiClientTest
    {
        private string database;
        private string email;
        private string password;

        [OneTimeSetUp]
        public void Setup()
        {
            DotNetEnv.Env.Load();
            database = System.Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
            email = System.Environment.GetEnvironmentVariable("BUGSPLAT_EMAIL");
            password = System.Environment.GetEnvironmentVariable("BUGSPLAT_PASSWORD");
        }

        [Test]
        public void BugSplatApiClient_Constructor_ShouldThrowIfEmailIsNullOrEmpty()
        {
            Assert.Throws<ArgumentException>(() => new BugSplatApiClient(null, password, HttpClientFactory.Default));
        }

        [Test]
        public void BugSplatApiClient_Constructor_ShouldThrowIfPasswordIsNullOrEmpty()
        {
            Assert.Throws<ArgumentException>(() => new BugSplatApiClient(email, null, HttpClientFactory.Default));
        }

        [Test]
        public void BugSplatApiClient_Constructor_ShouldThrowIfHttpClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new BugSplatApiClient(email, password, null));
        }

        [Test]
        public void BugSplatApiClient_Create_ShouldReturnBugSplatApiClient()
        {
            var result = BugSplatApiClient.Create(email, password);
            Assert.True(result is BugSplatApiClient);
        }

        [Test]
        public void BugSplatApiClient_Authenticate_ShouldCallPostAsyncWithUrlAndFormData()
        {
            var expectedUri = "https://app.bugsplat.com/api/authenticatev3";
            var expectedFormDataParams = new List<string>() { "name=email", email, "name=password", password };
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new BugSplatApiClient(email, password, httpClientFactory);

            var result = sut.Authenticate().Result;

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
        public void BugSplatApiClient_Authenticate_ShouldSetAuthenticatedTrue()
        {
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new BugSplatApiClient(email, password, httpClientFactory);

            Assert.False(sut.Authenticated);

            var result = sut.Authenticate().Result;

            Assert.True(sut.Authenticated);
        }

        [Test]
        public void BugSplatApiClient_Authenticate_ShouldThrowIfRequestFails()
        {
            var mockHttp = CreateMockHttpMessageHandlerForAuthenticateFail();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new BugSplatApiClient(email, password, httpClientFactory);

            Assert.False(sut.Authenticated);
            Assert.ThrowsAsync<HttpRequestException>(async () => { var result = await sut.Authenticate(); });
        }

        [Test]
        public void BugSplatApiClient_Authenticate_ShouldNotSetAuthenticatedTrueIfRequestFails()
        {
            var mockHttp = CreateMockHttpMessageHandlerForAuthenticateFail();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new BugSplatApiClient(email, password, httpClientFactory);

            try
            {
                Assert.False(sut.Authenticated);
                var result = sut.Authenticate().Result;
                Assert.Fail("Authenticate was supposed to throw!");
            }
            catch
            {
                Assert.False(sut.Authenticated);
            }
        }

        [Test]
        public void BugSplatApiClient_PostAsync_ShouldMakeRequestWithXsrfTokenHeader()
        {
            var xsrfToken = "xsrfTolkien!";
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess(xsrfToken);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new BugSplatApiClient(email, password, httpClientFactory);
            var authenticateResult = sut.Authenticate().Result;

            var postResult = sut.PostAsync("/xyz", new MultipartFormDataContent()).Result;

            mockHttp.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                        && ContainsHeader(req.Headers, "xsrf-token", xsrfToken)
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void BugSplatApiClient_GetAsync_ShouldMakeRequestWithXsrfTokenHeader()
        {
            var xsrfToken = "xsrfTolkien!";
            var expectedFormDataParams = new List<string>() { "name=email", email, "name=password", password };
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess(xsrfToken);
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new BugSplatApiClient(email, password, httpClientFactory);
            var authenticateResult = sut.Authenticate().Result;

            var postResult = sut.GetAsync("/xyz").Result;

            mockHttp.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get
                        && ContainsHeader(req.Headers, "xsrf-token", xsrfToken)
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        private Mock<HttpMessageHandler> CreateMockHttpClientForAuthenticateSuccess(string xsrfToken = "abc123")
        {
            var response = new HttpResponseMessage();
            response.Content = new StringContent("");
            response.StatusCode = HttpStatusCode.OK;
            response.Headers.Add("Set-Cookie", $"xsrf-token:{xsrfToken}; some-other-cookie:xyz321");
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

        private Mock<HttpMessageHandler> CreateMockHttpMessageHandlerForAuthenticateFail()
        {
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(
                   new HttpResponseMessage()
                   {
                       StatusCode = HttpStatusCode.BadRequest,
                       Content = new StringContent("")
                   }
                )
               .Verifiable();
 
            return handlerMock;
        }
    }
}