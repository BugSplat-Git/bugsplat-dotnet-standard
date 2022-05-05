using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BugSplatDotNetStandard;
using BugSplatDotNetStandard.Api;
using BugSplatDotNetStandard.Http;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Tests.HttpContentVerifiers;
using static Tests.StackTraceFactory;

namespace Tests
{
    public class VersionsClientTest
    {
        private string database;
        private string clientId;
        private string clientSecret;

        [OneTimeSetUp]
        public void Setup()
        {
            DotNetEnv.Env.Load();
            database = System.Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
            clientId = System.Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
            clientSecret = System.Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");
        }


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
        public void VersionsClient_UploadSymbolsFile_ShouldUploadSymbolFile()
        {
            var application = "my-net-crasher";
            var version = Guid.NewGuid().ToString();
            var symbolFileInfo = new FileInfo("Files/myConsoleCrasher.pdb");
            var oauth2ApiClient = OAuth2ApiClient.Create(clientId, clientSecret);
            var sut = VersionsClient.Create(oauth2ApiClient);
            var authenticateResult = oauth2ApiClient.Authenticate().Result;
            
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
    }
}