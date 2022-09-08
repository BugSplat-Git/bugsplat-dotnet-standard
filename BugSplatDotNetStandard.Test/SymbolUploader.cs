using System.Collections.Generic;
using System.IO;
using System.Net;
using BugSplatDotNetStandard;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class SymbolUploaderTest
    {
        private string database;
        private string email;
        private string password;
        private string clientId;
        private string clientSecret;

        [OneTimeSetUp]
        public void Setup()
        {
            DotNetEnv.Env.Load();
            database = System.Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
            email = System.Environment.GetEnvironmentVariable("BUGSPLAT_EMAIL");
            password = System.Environment.GetEnvironmentVariable("BUGSPLAT_PASSWORD");
            clientId = System.Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_ID");
            clientSecret = System.Environment.GetEnvironmentVariable("BUGSPLAT_CLIENT_SECRET");
        }

        [Test]
        public void SymbolUploader_UploadSymbolFile_ShouldUploadSymbolFileToBugSplat()
        {
            var sut = SymbolUploader.CreateSymbolUploader(email, password);
            var symbolFileInfo = new FileInfo("Files/myConsoleCrasher.exe");
            var response = sut.UploadSymbolFile(
                database,
                "myConsoleCrasher",
                "2022.5.2.0",
                symbolFileInfo
            ).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void SymbolUploader_UploadSymbolFiles_ShouldUploadSymbolFilesToBugSplat()
        {
            var sut = SymbolUploader.CreateOAuth2SymbolUploader(clientId, clientSecret);
            var symbolFileInfos = new List<FileInfo>()
            {
                new FileInfo("Files/myConsoleCrasher.exe"),
                new FileInfo("Files/myConsoleCrasher.pdb")
            };
            var responses = sut.UploadSymbolFiles(
                database,
                "myConsoleCrasher",
                "2022.5.2.0",
                symbolFileInfos
            ).Result;

            foreach(var response in responses)
            {
                var body = response.Content.ReadAsStringAsync().Result;
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Test]
        public void SymbolUploader_UploadSymbolFileWithSignature_ShouldUploadSymbolFileToBugSplat()
        {
            var sut = SymbolUploader.CreateSymbolUploader(email, password);
            var symbolFileInfo = new FileInfo("Files/myConsoleCrasher.exe");
            var response = sut.UploadSymbolFileWithSignature(
                database,
                "myConsoleCrasher",
                "2022.5.2.0",
                symbolFileInfo,
                "62702A2F26000"
            ).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}