using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using BugSplatDotNetStandard.Http;
using NUnit.Framework;

namespace Tests
{
    // A failed request reaches the caller unchanged: nothing between the public methods and
    // HttpClient catches it.
    [TestFixture]
    [NonParallelizable]
    public class BugSplatFailureTest
    {
        private IHttpClientFactory originalHttpClientFactory;
        private string tempFile;

        [SetUp]
        public void SetUp()
        {
            originalHttpClientFactory = HttpClientFactory.Default;
            tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, "BugSplat failure test");
        }

        [TearDown]
        public void TearDown()
        {
            HttpClientFactory.Default = originalHttpClientFactory;
            File.Delete(tempFile);
        }

        public static IEnumerable<TestCaseData> Posts()
        {
            yield return new TestCaseData((Func<BugSplat, string, Task>)((bugsplat, _) => bugsplat.Post("stack trace"))).SetName("Post(string)");
            yield return new TestCaseData((Func<BugSplat, string, Task>)((bugsplat, _) => bugsplat.Post(new Exception("BugSplat rocks!")))).SetName("Post(Exception)");
            yield return new TestCaseData((Func<BugSplat, string, Task>)((bugsplat, file) => bugsplat.Post(new FileInfo(file), new MinidumpPostOptions()))).SetName("Post(minidump)");
            yield return new TestCaseData((Func<BugSplat, string, Task>)((bugsplat, file) => bugsplat.Post(new FileInfo(file), new XmlPostOptions()))).SetName("Post(xml)");
            yield return new TestCaseData((Func<BugSplat, string, Task>)((bugsplat, file) => bugsplat.Post(new FileInfo(file), new BugSplatPostOptions()))).SetName("Post(crash file)");
            yield return new TestCaseData((Func<BugSplat, string, Task>)((bugsplat, _) => bugsplat.PostFeedback("feedback title"))).SetName("PostFeedback");
        }

        [TestCaseSource(nameof(Posts))]
        public void WhenTheRequestCantBeSent_ThrowsTheHttpRequestException(Func<BugSplat, string, Task> post)
        {
            var expected = new HttpRequestException("No such host is known");
            HttpClientFactory.Default = new FakeHttpClientFactory(new HttpClient(new FaultingHandler(expected)));

            var actual = Assert.ThrowsAsync<HttpRequestException>(() => post(new BugSplat("fred", "my-app", "1.0.0"), tempFile));

            Assert.AreSame(expected, actual);
        }

        [TestCaseSource(nameof(Posts))]
        public void WhenTheRequestTimesOut_ThrowsTheTaskCanceledException(Func<BugSplat, string, Task> post)
        {
            var expected = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
            HttpClientFactory.Default = new FakeHttpClientFactory(new HttpClient(new FaultingHandler(expected)));

            var actual = Assert.ThrowsAsync<TaskCanceledException>(() => post(new BugSplat("fred", "my-app", "1.0.0"), tempFile));

            Assert.AreSame(expected, actual);
        }

        [TestCaseSource(nameof(Posts))]
        public void WhenBugSplatReturnsAnErrorStatus_ThrowsWithItsResponse(Func<BugSplat, string, Task> post)
        {
            HttpClientFactory.Default = new FakeHttpClientFactory(new HttpClient(new RespondingHandler(HttpStatusCode.InternalServerError, "BugSplat is down")));

            var actual = Assert.ThrowsAsync<HttpRequestException>(() => post(new BugSplat("fred", "my-app", "1.0.0"), tempFile));

            StringAssert.Contains("BugSplat is down", actual.Message);
        }

        private class FaultingHandler : HttpMessageHandler
        {
            private readonly Exception exception;

            public FaultingHandler(Exception exception)
            {
                this.exception = exception;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromException<HttpResponseMessage>(exception);
            }
        }

        private class RespondingHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly string body;

            public RespondingHandler(HttpStatusCode status, string body)
            {
                this.status = status;
                this.body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
            }
        }
    }
}
