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
        public void BugSplat_Post_ShouldPostExceptionToBugSplat()
        {
            try
            {
                new Foo(new Bar(new Baz())).SampleStackFrame();
            }
            catch (Exception ex)
            {
                var sut = new BugSplat("fred", "MyDotNetStandardCrasher", "1.0");

                var options = new BugSplatPostOptions()
                {
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
        public void BugSplat_PostMinidump_ShouldPostMinidumpToBugSplat()
        {
            var sut = new BugSplat("fred", "myConsoleCrasher", "2021.4.23.0");

            var minidumpFileInfo = new FileInfo("minidump.dmp");
            // TODO BG https://github.com/BugSplat-Git/webroot/issues/459
            var options = new BugSplatPostOptions()
            {
                Description = "BugSplat rocks!",
                Email = "fred@bugsplat.com",
                User = "Fred",
                Key = "the key!"
            };
            options.AdditionalAttachments.Add(new FileInfo("attachment.txt"));
            var response = sut.Post(minidumpFileInfo, options).Result;
            var body = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
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