using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using BugSplatDotNetStandard.Utils;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class VersionsClientTest
    {
        const string database = "fred";

        [Test]
        public void VersionsClient_Constructor_ShouldThrowIfHttpClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new VersionsClient(null, S3ClientFactory.Default));
        }

        [Test]
        public void VersionsClient_Constructor_ShouldThrowIfS3ClientFactoryIsNullOrEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new VersionsClient(new BugSplatApiClient("fred", "******", HttpClientFactory.Default), null));
        }

        [Test]
        public void VersionsClient_Create_ReturnsVersionsClient()
        {
            var versionsClient = VersionsClient.Create(new BugSplatApiClient("fred", "******", HttpClientFactory.Default));
            Assert.True(versionsClient is VersionsClient);
        }

        [Test]
        public void VersionsClient_UploadSymbolFile_ShouldThrowIfResponseIsMissingUrl()
        {
            var sut = CreateVersionsClientForPresignedUrlResponse("{ \"code\": 1 }");

            var ex = Assert.ThrowsAsync<Exception>(async () => await UploadSymbolFile(sut));

            Assert.AreEqual("Failed to parse symbol upload url", ex.Message);
        }

        [Test]
        public void VersionsClient_UploadSymbolFile_ShouldIncludeMessageWhenUrlIsMissing()
        {
            var sut = CreateVersionsClientForPresignedUrlResponse("{ \"message\": \"quota exceeded\" }");

            var ex = Assert.ThrowsAsync<Exception>(async () => await UploadSymbolFile(sut));

            Assert.AreEqual("Failed to parse symbol upload url: quota exceeded", ex.Message);
        }

        [Test]
        public void VersionsClient_UploadSymbolFile_ShouldNotMatchNestedUrl()
        {
            // A nested url used to satisfy the root level lookup via XPath //key
            var sut = CreateVersionsClientForPresignedUrlResponse("{ \"error\": { \"url\": \"https://nested.example.com\" } }");

            var ex = Assert.ThrowsAsync<Exception>(async () => await UploadSymbolFile(sut));

            Assert.AreEqual("Failed to parse symbol upload url", ex.Message);
        }

        [Test]
        public void VersionsClient_UploadSymbolFile_ShouldWrapMalformedJson()
        {
            var sut = CreateVersionsClientForPresignedUrlResponse("not json");

            var ex = Assert.ThrowsAsync<Exception>(async () => await UploadSymbolFile(sut));

            Assert.AreEqual("Failed to parse symbol upload url", ex.Message);
            Assert.NotNull(ex.InnerException);
        }

        private static Task<HttpResponseMessage> UploadSymbolFile(VersionsClient sut)
        {
            return sut.UploadSymbolFile(database, "my-net-crasher", "1.0.0", new FileInfo("Files/myConsoleCrasher.pdb"));
        }

        private static VersionsClient CreateVersionsClientForPresignedUrlResponse(string responseContent)
        {
            var mockApiClient = new Mock<IBugSplatApiClient>();
            mockApiClient
                .SetupGet(c => c.Authenticated)
                .Returns(true);
            mockApiClient
                .Setup(c => c.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent>()))
                .ReturnsAsync(new HttpResponseMessage() { Content = new StringContent(responseContent) });

            return new VersionsClient(mockApiClient.Object, FakeS3ClientFactory.CreateMockS3ClientFactory());
        }
    }

    [TestFixture]
    class VersionsClientIntegrationTest
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
        public void VersionsClient_UploadSymbolsFile_ShouldUploadSymbolFile()
        {
            var application = "my-net-crasher";
            var version = Guid.NewGuid().ToString();
            var symbolFileInfo = new FileInfo("Files/myConsoleCrasher.pdb");
            var oauth2ApiClient = OAuth2ApiClient.Create(clientId, clientSecret)
                .Authenticate()
                .Result;
            var sut = VersionsClient.Create(oauth2ApiClient);

            var uploadResult = sut.UploadSymbolFile(
                database,
                application,
                version,
                symbolFileInfo
            ).Result;

            var versionsResult = sut.GetVersions(database).Result;
            var jArray = JArray.Parse(versionsResult.Content.ReadAsStringAsync().Result);
            var response = jArray[0];
            var rows = response["Rows"].Select(rows => rows).ToList();
            var row = rows.Single(row => ((string)row["version"]).Equals(version));
            Assert.AreEqual(application, (string)row["appName"]);
            Assert.AreEqual(version, (string)row["version"]);
            Assert.Greater(double.Parse((string)row["size"]), 0);
        }

        [Test]
        public void VersionsClient_UploadSymbolsFile_ShouldDeleteZipFileAfterUpload()
        {
            var application = "my-net-crasher";
            var version = Guid.NewGuid().ToString();
            var zipFileFullName = string.Empty;
            var symbolFileInfo = new FileInfo("Files/myConsoleCrasher.pdb");
            var mockApiClient = CreateMockBugSplatApiClient();
            var mockS3ClientFactory = FakeS3ClientFactory.CreateMockS3ClientFactory();
            var realTempFileFactory = new TempFileFactory();
            var mockTempFileFactory = new Mock<ITempFileFactory>();
            mockTempFileFactory
                .Setup(factory => factory.CreateTempZip(It.IsAny<IEnumerable<FileInfo>>()))
                .Returns((IEnumerable<FileInfo> files) =>
                {
                    var result = realTempFileFactory.CreateTempZip(files);
                    zipFileFullName = result.File.FullName;
                    return result;
                });
            var sut = new VersionsClient(mockApiClient, mockS3ClientFactory);
            sut.TempFileFactory = mockTempFileFactory.Object;

            var uploadResult = sut.UploadSymbolFile(
                database,
                application,
                version,
                symbolFileInfo
            ).Result;

            Assert.False(File.Exists(zipFileFullName));
        }

        private IBugSplatApiClient CreateMockBugSplatApiClient()
        {
            var presignedUrlResponse = new HttpResponseMessage()
            {
                Content = new StringContent("{ \"url\": \"https://x.com\" }")
            };
            var mockApiClient = new Mock<IBugSplatApiClient>();
            mockApiClient
                .SetupGet(c => c.Authenticated)
                .Returns(true);
            mockApiClient
                .Setup(c => c.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent>()))
                .ReturnsAsync(presignedUrlResponse);
            return mockApiClient.Object;
        }
    }
}