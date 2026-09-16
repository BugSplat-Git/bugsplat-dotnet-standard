using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BugSplatDotNetStandard.Http;
using NUnit.Framework;

namespace Tests
{
    [TestFixture]
    public class JsonObjectTest
    {

        [Test]
        public void JsonObject_GetValue_ShouldReturnValueForTopLevelKey()
        {
            var expected = "https://bugsplat.com";
            var json = $@"{{ ""url"": ""{expected}"" }}";
            var obj = new JsonObject(json);

            var result = obj.GetValue("url");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void JsonObject_GetValue_ShouldReturnValueForNestedKey()
        {
            var expected = "rocks!";
            var json = $@"{{ ""bug"": {{  ""splat"": ""{expected}"" }} }}";
            var obj = new JsonObject(json);

            var result = obj.GetValue("bug", "splat");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void JsonObject_GetValue_ShouldThrowForAbsentKey()
        {
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var ex = Assert.Throws<KeyNotFoundException>(() => obj.GetValue("message"));

            StringAssert.Contains("message", ex.Message);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnValueForTopLevelKey()
        {
            var expected = "https://bugsplat.com";
            var json = $@"{{ ""url"": ""{expected}"" }}";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue("url");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnValueForNestedKey()
        {
            var expected = "rocks!";
            var json = $@"{{ ""bug"": {{  ""splat"": ""{expected}"" }} }}";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue("bug", "splat");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnNullForAbsentKey()
        {
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue("message");

            Assert.IsNull(result);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnNullForAbsentNestedKey()
        {
            var json = @"{ ""bug"": { ""splat"": ""rocks!"" } }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue("bug", "crash");

            Assert.IsNull(result);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnNullForMalformedJson()
        {
            var obj = new JsonObject("not json");

            var result = obj.TryGetValue("url");

            Assert.IsNull(result);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldNotThrowInternallyForAbsentKey()
        {
            // A missing key is a lookup miss, not an exception. Every successful crash post
            // asks for "message" and doesn't find it, so a throw here stops any attached
            // debugger inside the SDK instead of at the customer's crash.
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var thrown = RecordFirstChanceExceptions(() => obj.TryGetValue("message"));

            Assert.IsEmpty(thrown, $"TryGetValue threw internally: {string.Join(", ", thrown)}");
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldNotThrowInternallyForPresentKey()
        {
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var thrown = RecordFirstChanceExceptions(() => obj.TryGetValue("url"));

            Assert.IsEmpty(thrown, $"TryGetValue threw internally: {string.Join(", ", thrown)}");
        }

        // Records every exception raised on this thread while action runs, caught or not
        private static List<string> RecordFirstChanceExceptions(Action action)
        {
            var thrown = new List<string>();
            var threadId = Environment.CurrentManagedThreadId;
            EventHandler<FirstChanceExceptionEventArgs> handler = (sender, args) =>
            {
                if (Environment.CurrentManagedThreadId == threadId)
                {
                    thrown.Add(args.Exception.ToString());
                }
            };

            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                action();
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            return thrown;
        }
    }

    [TestFixture]
    public class JsonSerializerTest
    {
        [Test]
        public void JsonSerializer_Serialize_ShouldReturnNull()
        {
            var result = JsonSerializer.Serialize(null);

            Assert.AreEqual("null", result);
        }

        [Test]
        public void JsonSerializer_Serialize_ShouldEscapeChars()
        {
            var key = "key";
            var value = "\"\\\b\f\n\r\t";
            var expectedValue = "\\\"\\\\\\b\\f\\n\\r\\t";
            var expected = $@"{{""{key}"":""{expectedValue}""}}";
            var dictionary = new Dictionary<string, string>()
            {
                { key, value }
            };

            var result = JsonSerializer.Serialize(dictionary);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void JsonSerializer_Serialize_ShouldEncodeSpecialChars()
        {
            var key = "key";
            var value = "你好"; // "Hello" in Chinese
            var expectedValue = "\\u4F60\\u597D";
            var expected = $@"{{""{key}"":""{expectedValue}""}}";
            var dictionary = new Dictionary<string, string>()
            {
                { key, value }
            };

            var result = JsonSerializer.Serialize(dictionary);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void JsonSerializer_Serialize_ShouldConvertDictionaryToJsonString()
        {
            var attributeKey0 = "key0";
            var attributeValue0 = "value0";
            var attributeKey1 = "key1";
            var attributeValue1 = "value0";
            var expected = $@"{{""{attributeKey0}"":""{attributeValue0}"",""{attributeKey1}"":""{attributeValue1}""}}";
            var dictionary = new Dictionary<string, string>()
            {
                { attributeKey0, attributeValue0 },
                { attributeKey1, attributeValue1 },
            };

            var result = JsonSerializer.Serialize(dictionary);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void JsonSerializer_Serialize_ShouldEmitNullForNullValues()
        {
            var dictionary = new Dictionary<string, string>()
            {
                { "key", null }
            };

            var result = JsonSerializer.Serialize(dictionary);

            Assert.AreEqual(@"{""key"":null}", result);
        }

        [Test]
        public void JsonSerializer_EscapeJsonString_ShouldBePubliclyAccessible()
        {
            var result = JsonSerializer.EscapeJsonString("hello \"world\"");

            Assert.AreEqual("hello \\\"world\\\"", result);
        }

        [Test]
        public void JsonSerializer_EscapeJsonString_ShouldHandleEmoji()
        {
            var result = JsonSerializer.EscapeJsonString("😀");

            Assert.AreEqual("\\uD83D\\uDE00", result);
        }
    }
}
