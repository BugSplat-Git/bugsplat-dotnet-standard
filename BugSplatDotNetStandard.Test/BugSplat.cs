using BugSplatDotNetStandard;
using NUnit.Framework;
using System;
using System.IO;
using System.Net;

namespace Tests
{
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
        public void BugSplat_Post_ShouldPostExceptionToBugSplat()
        {
            try
            {
                new Foo(new Bar(new Baz())).SampleStackFrame();
            }
            catch (Exception ex)
            {
                var sut = new BugSplat("fred", "MyDotNetStandardCrasher", "1.0");
                sut.ExceptionType = BugSplat.ExceptionTypeId.Unity;
                sut.Description = "Default description - overridden";
                sut.Email = "default@bugsplat.com - overridden";
                sut.User = "Default - overridden";
                sut.Key = "Default - overridden";
                var options = new ExceptionPostOptions()
                {
                    ExceptionType = BugSplat.ExceptionTypeId.DotNetStandard,
                    Description = "BugSplat rocks!",
                    Email = "fred@bugsplat.com",
                    User = "Fred",
                    Key = "the key!"
                };
                options.AdditionalAttachments.Add(new FileInfo("attachment.txt"));
                var response = sut.Post(ex, options).Result;
                var body = response.Content.ReadAsStringAsync().Result;

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }
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
        public void BugSplat_Post_ShouldPostMinidumpToBugSplat()
        {
            var sut = new BugSplat("fred", "myConsoleCrasher", "2021.4.23.0");
            var minidumpFileInfo = new FileInfo("minidump.dmp");
            sut.MinidumpType = BugSplat.MinidumpTypeId.WindowsNative;
            sut.Description = "Default description - overridden";
            sut.Email = "default@bugsplat.com - overridden";
            sut.User = "Default - overridden";
            sut.Key = "Default - overridden";
            var options = new MinidumpPostOptions()
            {
                MinidumpType = BugSplat.MinidumpTypeId.UnityNativeWindows,
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                User = "Fred",
                Key = "the key!"
            };
            options.AdditionalAttachments.Add(new FileInfo("attachment.txt"));

            var md5 = "B7AAAD5CD414C986C98B7560478DB0A2";
            
            var response = sut.Post(minidumpFileInfo, options, md5).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        }

        [Test]
        public void BugSplat_Post_ShouldThrowIfMinidumpFileInfoIsNull()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                FileInfo fileInfo = null;
                var bugsplat = new BugSplat("fred", "my-app", "1.0.0");
                await bugsplat.Post(fileInfo);
            });
        }

        [Test]
        public void BugSplat_Post_ShouldPostStackTraceToBugSplat()
        {
            var sut = new BugSplat("fred", "MyUnityCrasher", "1.0");
            sut.ExceptionType = BugSplat.ExceptionTypeId.Unity;
            sut.Description = "Default description - overridden";
            sut.Email = "default@bugsplat.com - overridden";
            sut.User = "Default - overridden";
            sut.Key = "Default - overridden";
            var stackTrace = @"Exception: BugSplat rocks!
                Main.ThrowException () (at Assets/Main.cs:75)
                Main.SampleStackFrame2 () (at Assets/Main.cs:95)
                Main.SampleStackFrame1 () (at Assets/Main.cs:90)
                Main.SampleStackFrame0 () (at Assets/Main.cs:85)
                Main.GenerateSampleStackFramesAndThrow () (at Assets/Main.cs:80)
                Main.Update() (at Assets/Main.cs:69)";
            var options = new ExceptionPostOptions()
            {
                ExceptionType = BugSplat.ExceptionTypeId.UnityLegacy,
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                User = "Fred",
                Key = "the key!"
            };
            options.AdditionalAttachments.Add(new FileInfo("attachment.txt"));
            var response = sut.Post(stackTrace, options).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
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