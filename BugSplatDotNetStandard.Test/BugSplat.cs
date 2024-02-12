using BugSplatDotNetStandard;
using NUnit.Framework;
using System;
using System.IO;
using System.Net;
using System.Threading;
using static Tests.StackTraceFactory;

namespace Tests
{
    [TestFixture]
    public class BugSplatTest
    {
        [Test]
        public void BugSplat_Constructor_ShouldThrowIfDatabaseIsNull()
        {
            Assert.Throws<ArgumentException>(() => new BugSplat(null, "my-app", "1.0.0"));
        }

        [Test]
        public void BugSplat_Constructor_ShouldThrowIfApplicationIsNull()
        {
            Assert.Throws<ArgumentException>(() => new BugSplat("fred", null, "1.0.0"));
        }

        [Test]
        public void BugSplat_Constructor_ShouldThrowIfVersionIsNull()
        {
            Assert.Throws<ArgumentException>(() => new BugSplat("fred", "my-app", null));
        }

        [Test]
        public void BugSplat_Post_ShouldThrowIfExIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                Exception ex = null;
                var bugsplat = new BugSplat("fred", "my-app", "1.0.0");
                await bugsplat.Post(ex);
            });
        }

        [Test]
        public void BugSplat_Post_ShouldThrowIfStackTraceFileInfoIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                string stackTrace = null;
                var bugsplat = new BugSplat("fred", "my-app", "1.0.0");
                await bugsplat.Post(stackTrace);
            });
        }
    }

    [TestFixture]
    public class BugSplatIntegrationTest
    {
        private string database;
        private string email;
        private string password;

        [OneTimeSetUp]
        public void BeforeAll()
        {
            DotNetEnv.Env.Load();
            database = System.Environment.GetEnvironmentVariable("BUGSPLAT_DATABASE");
            email = System.Environment.GetEnvironmentVariable("BUGSPLAT_EMAIL");
            password = System.Environment.GetEnvironmentVariable("BUGSPLAT_PASSWORD");
        }

        [SetUp]
        public void BeforeEach()
        {
            Thread.Sleep(2000); // Prevent crash post rate limiting
        }

        [Test]
        public void BugSplat_Post_ShouldPostExceptionToBugSplat()
        {
            try
            {
                new Foo(new Bar(new Baz())).SampleStackFrame();
            }
            catch (Exception ex)
            {
                var sut = new BugSplat(database, "MyDotNetStandardCrasher", "1.0");
                sut.ExceptionType = BugSplat.ExceptionTypeId.Unity;
                sut.Description = "Default description - overridden";
                sut.Email = "default@bugsplat.com - overridden";
                sut.User = "Default - overridden";
                sut.Key = "Default - overridden";
                sut.Notes = "Default - overridden";
                var options = new ExceptionPostOptions()
                {
                    ExceptionType = BugSplat.ExceptionTypeId.DotNetStandard,
                    Description = "BugSplat rocks!",
                    Email = "fred@bugsplat.com",
                    User = "Fred",
                    Key = "the key!",
                    Notes = "the notes!"
                };
                options.Attachments.Add(new FileInfo("Files/attachment.txt"));
                var response = sut.Post(ex, options).Result;
                var body = response.Content.ReadAsStringAsync().Result;

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
        }

        [Test]
        public void BugSplat_Post_ShouldPostMinidumpToBugSplat()
        {
            var sut = new BugSplat(database, "myConsoleCrasher", "2022.5.2.0");
            var minidumpFileInfo = new FileInfo("Files/minidump.dmp");
            sut.MinidumpType = BugSplat.MinidumpTypeId.WindowsNative;
            sut.Description = "Default description - overridden";
            sut.Email = "default@bugsplat.com - overridden";
            sut.User = "Default - overridden";
            sut.Key = "Default - overridden";
            sut.Notes = "Default - overridden";
            var options = new MinidumpPostOptions()
            {
                MinidumpType = BugSplat.MinidumpTypeId.UnityNativeWindows,
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                User = "Fred",
                Key = "the key!",
                Notes = "the notes!"
            };
            options.Attachments.Add(new FileInfo("Files/attachment.txt"));

            var response = sut.Post(minidumpFileInfo, options).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void BugSplat_Post_ShouldThrowIfMinidumpFileInfoIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                var bugsplat = new BugSplat("fred", "my-app", "1.0.0");
                await bugsplat.Post(null, MinidumpPostOptions.Create(bugsplat));
            });
        }

        [Test]
        public void BugSplat_Post_ShouldPostStackTraceToBugSplat()
        {
            var sut = new BugSplat(database, "MyUnityCrasher", "1.0");
            sut.ExceptionType = BugSplat.ExceptionTypeId.Unity;
            sut.Description = "Default description - overridden";
            sut.Email = "default@bugsplat.com - overridden";
            sut.User = "Default - overridden";
            sut.Key = "Default - overridden";
            sut.Notes = "Default - overridden";
            var stackTrace = CreateStackTrace();
            var options = new ExceptionPostOptions()
            {
                ExceptionType = BugSplat.ExceptionTypeId.UnityLegacy,
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                User = "Fred",
                Key = "the key!",
                Notes = "the notes!"
            };
            options.Attachments.Add(new FileInfo("Files/attachment.txt"));
            var response = sut.Post(stackTrace, options).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void BugSplat_Post_ShouldThrowIfStackTraceIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                var bugsplat = new BugSplat("fred", "my-app", "1.0.0");
                await bugsplat.Post(null as string, ExceptionPostOptions.Create(bugsplat));
            });
        }

        [Test]
        public void BugSplat_Post_ShouldXmlReportToBugSplat()
        {
            var sut = new BugSplat(database, "MyXmlCrasher", "1.0");
            sut.XmlType = BugSplat.XmlTypeId.Asan;
            sut.Description = "Default description - overridden";
            sut.Email = "default@bugsplat.com - overridden";
            sut.User = "Default - overridden";
            sut.Key = "Default - overridden";
            sut.Notes = "Default - overridden";
            var stackTrace = new FileInfo("Files/bsCrashReport.xml");
            var options = new XmlPostOptions()
            {
                XmlType = BugSplat.XmlTypeId.Xml,
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                User = "Fred",
                Key = "the key!",
                Notes = "the notes!"
            };
            options.Attachments.Add(new FileInfo("Files/attachment.txt"));
            var response = sut.Post(stackTrace, options).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void BugSplat_Post_ShouldThrowIfXmlReportIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                var bugsplat = new BugSplat("fred", "my-app", "1.0.0");
                await bugsplat.Post(null, XmlPostOptions.Create(bugsplat));
            });
        }
    }

    public class Foo
    {
        private Bar _bar;

        public Foo(Bar bar)
        {
            _bar = bar;
        }

        public void SampleStackFrame()
        {
            _bar.SampleStackFrame();
        }
    }

    public class Bar
    {
        private Baz _baz;

        public Bar(Baz baz)
        {
            _baz = baz;
        }

        public void SampleStackFrame()
        {
            _baz.SampleStackFrame();
        }
    }

    public class Baz
    {
        public void SampleStackFrame()
        {
            throw new Exception("BugSplat!");
        }
    }
}