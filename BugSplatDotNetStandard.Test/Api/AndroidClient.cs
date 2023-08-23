using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class AndroidClientTest
    {
        const string database = "fred";

        [Test]
        public void AndroidClient_Constructor_ShouldThrowIfHttpClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new AndroidClient(null));
        }

        [Test]
        public void AndroidClient_Create_ShouldThrowIfDatabaseIsEmpty()
        {
            Assert.ThrowsAsync<ArgumentException>(async () => await AndroidClient.Create("", new BugSplatApiClient("fred", "******", HttpClientFactory.Default)));
        }

        [Test]
        public void AndroidClient_Create_ShouldThrowIfBugSplatApiClientIsEmpty()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await AndroidClient.Create("fred", null));
        }
    }
    [TestFixture]
    class AndroidClientIntegrationTest
    {
        private string database;
        private string clientId;
        private string clientSecret;

        [OneTimeSetUp]
        public void Setup()
        {
            DotNetEnv.Env.Load();
            database = Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
            clientId = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
            clientSecret = Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");
        }

        [Test]
        public async Task AndroidClient_Create_ReturnsAndroidClient()
        {
            var database = "fred";
            var androidClient = await AndroidClient.Create(database, new OAuth2ApiClient(clientId, clientSecret, HttpClientFactory.Default));
            Assert.True(androidClient is AndroidClient);
        }

        [Test]
        public async Task AndroidClient_UploadBinaryFile_ShouldUploadBinaryFile()
        {
            var binaryFileInfo = new FileInfo("Files/libnative-lib.so");
            var oauth2ApiClient = OAuth2ApiClient.Create(clientId, clientSecret).Authenticate().Result;
            var sut = AndroidClient.Create(database, oauth2ApiClient).Result;

            var uploadResult = await sut.UploadBinaryFile(binaryFileInfo);
            var content = uploadResult.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, uploadResult.StatusCode);
            StringAssert.Contains("MODULE Linux arm64 00FAE6D9DBA9BB42DCB866F70473AABA0 libnative-lib.so", content);
        }
    }
}