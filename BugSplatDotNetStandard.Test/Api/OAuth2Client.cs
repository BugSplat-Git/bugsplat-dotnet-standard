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
    public class OAuth2ClientTest
    {
        private string clientId;
        private string clientSecret;

        [OneTimeSetUp]
        public void Setup()
        {
            DotNetEnv.Env.Load();
            clientId = System.Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
            clientSecret = System.Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");
        }

        [Test]
        public void OAuth2ApiClient_Constructor_ShouldThrowIfClientIdIsNullOrEmpty()
        {
            Assert.Throws<ArgumentException>(() => new OAuth2ApiClient(null, clientSecret, HttpClientFactory.Default));
        }

        [Test]
        public void OAuth2ApiClient_Constructor_ShouldThrowIfClientSecretIsNullOrEmpty()
        {
            Assert.Throws<ArgumentException>(() => new OAuth2ApiClient(clientId, null, HttpClientFactory.Default));
        }

        [Test]
        public void OAuth2ApiClient_Constructor_ShouldThrowIfHttpClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new OAuth2ApiClient(clientId, clientSecret, null));
        }

        [Test]
        public void OAuth2ApiClient_Create_ShouldReturnOAuth2ApiClient()
        {
            var result = OAuth2ApiClient.Create(clientId, clientSecret);
            Assert.True(result is OAuth2ApiClient);
        }

        [Test]
        public void OAuth2ApiClient_Authenticate_ShouldCallPostAsyncWithUrlAndFormData()
        {
            var expectedUri = "https://app.bugsplat.com/oauth2/authorize";
            var expectedFormDataParams = new List<string>() {
                "name=client_id", clientId,
                "name=client_secret", clientSecret,
                "name=scope", "restricted",
                "name=grant_type", "client_credentials"
            };
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new OAuth2ApiClient(clientId, clientSecret, httpClientFactory);

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
        public void OAuth2ApiClient_Authenticate_ShouldSetAuthenticatedTrue()
        {
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new OAuth2ApiClient(clientId, clientSecret, httpClientFactory);

            Assert.False(sut.Authenticated);

            var result = sut.Authenticate().Result;

            Assert.True(sut.Authenticated);
        }

        [Test]
        public void OAuth2ApiClient_Authenticate_ShouldThrowIfRequestFails()
        {
            var mockHttp = CreateMockHttpMessageHandlerForAuthenticateFail();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new OAuth2ApiClient(clientId, clientSecret, httpClientFactory);

            Assert.False(sut.Authenticated);
            Assert.ThrowsAsync<HttpRequestException>(async () => { var result = await sut.Authenticate(); });
        }

        [Test]
        public void OAuth2ApiClient_Authenticate_ShouldNotSetAuthenticatedTrueIfRequestFails()
        {
            var mockHttp = CreateMockHttpMessageHandlerForAuthenticateFail();
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new OAuth2ApiClient(clientId, clientSecret, httpClientFactory);

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
        public void OAuth2ApiClient_PostAsync_ShouldMakeRequestWithAuthorizationHeader()
        {
            var accessToken = "accessTolkien!";
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess(
                "{ \"access_token\": \"" + accessToken + "\", \"token_type\": \"Bearer\" }"
            );
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new OAuth2ApiClient(clientId, clientSecret, httpClientFactory);
            var authenticateResult = sut.Authenticate().Result;

            var postResult = sut.PostAsync("/xyz", new MultipartFormDataContent()).Result;

            mockHttp.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                        && ContainsHeader(req.Headers, "Authorization", $"Bearer {accessToken}")
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void OAuth2ApiClient_GetAsync_ShouldMakeRequestWithAuthorizationHeader()
        {
            var accessToken = "accessTolkien!";
            var mockHttp = CreateMockHttpClientForAuthenticateSuccess(
                "{ \"access_token\": \"" + accessToken + "\", \"token_type\": \"Bearer\" }"
            );
            var httpClient = new HttpClient(mockHttp.Object);
            var httpClientFactory = new FakeHttpClientFactory(httpClient);
            var sut = new OAuth2ApiClient(clientId, clientSecret, httpClientFactory);
            var authenticateResult = sut.Authenticate().Result;

            var postResult = sut.GetAsync("/xyz").Result;

            mockHttp.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get
                        && ContainsHeader(req.Headers, "Authorization", $"Bearer {accessToken}")
                ),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        private Mock<HttpMessageHandler> CreateMockHttpClientForAuthenticateSuccess(
            string responseContent = "{ \"access_token\": \"abc123\", \"token_type\": \"Bearer\" }"
        )
        {
            var response = new HttpResponseMessage();
            response.Content = new StringContent(responseContent);
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