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
        public void JsonObject_TryGetValue_ShouldReturnValueForTopLevelKey()
        {
            var expected = "https://bugsplat.com";
            var json = $@"{{ ""url"": ""{expected}"" }}";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "url");

            Assert.True(result);
            Assert.AreEqual(expected, value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnValueForNestedKey()
        {
            var expected = "rocks!";
            var json = $@"{{ ""bug"": {{ ""splat"": ""{expected}"" }} }}";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "bug", "splat");

            Assert.True(result);
            Assert.AreEqual(expected, value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldNotMatchNestedKeyForRootLevelLookup()
        {
            // The XPath //key this replaced matched a key anywhere in the document, so a
            // nested url satisfied a root level lookup and the wrong value was uploaded to
            var json = @"{ ""error"": { ""url"": ""https://nested.example.com"" } }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "url");

            Assert.False(result);
            Assert.IsNull(value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldNotMatchDeeperPathForShorterLookup()
        {
            var json = @"{ ""a"": { ""b"": { ""c"": ""deep"" } } }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "b", "c");

            Assert.False(result);
            Assert.IsNull(value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnFalseForAbsentKey()
        {
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "message");

            Assert.False(result);
            Assert.IsNull(value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnFalseForAbsentNestedKey()
        {
            var json = @"{ ""bug"": { ""splat"": ""rocks!"" } }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "bug", "crash");

            Assert.False(result);
            Assert.IsNull(value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnFalseForObjectAndArrayValues()
        {
            var json = @"{ ""obj"": { ""a"": ""b"" }, ""arr"": [ ""a"", ""b"" ] }";
            var obj = new JsonObject(json);

            Assert.False(obj.TryGetValue(out var objectValue, "obj"));
            Assert.IsNull(objectValue);
            Assert.False(obj.TryGetValue(out var arrayValue, "arr"));
            Assert.IsNull(arrayValue);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnFalseForNullValue()
        {
            var json = @"{ ""url"": null }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "url");

            Assert.False(result);
            Assert.IsNull(value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnNumbersAndBooleansAsText()
        {
            var json = @"{ ""count"": 42, ""ok"": true }";
            var obj = new JsonObject(json);

            Assert.True(obj.TryGetValue(out var count, "count"));
            Assert.AreEqual("42", count);
            Assert.True(obj.TryGetValue(out var ok, "ok"));
            Assert.AreEqual("true", ok);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldFindKeyThatIsNotAValidXmlName()
        {
            var expected = "https://bugsplat.com";
            var json = $@"{{ ""2fa url"": ""{expected}"" }}";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value, "2fa url");

            Assert.True(result);
            Assert.AreEqual(expected, value);
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldReturnFalseForEmptyPath()
        {
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var result = obj.TryGetValue(out var value);

            Assert.False(result);
            Assert.IsNull(value);
        }

        [Test]
        public void JsonObject_Constructor_ShouldThrowForMalformedJson()
        {
            Assert.Throws<System.Xml.XmlException>(() => new JsonObject("not json"));
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldNotThrowInternallyForAbsentKey()
        {
            // A missing key is a lookup miss, not an exception. Every successful crash post
            // asks for "message" and doesn't find it, so a throw here stops any attached
            // debugger inside the SDK instead of at the customer's crash.
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var thrown = RecordFirstChanceExceptions(() => obj.TryGetValue(out var value, "message"));

            Assert.IsEmpty(thrown, $"TryGetValue threw internally: {string.Join(", ", thrown)}");
        }

        [Test]
        public void JsonObject_TryGetValue_ShouldNotThrowInternallyForPresentKey()
        {
            var json = @"{ ""url"": ""https://bugsplat.com"" }";
            var obj = new JsonObject(json);

            var thrown = RecordFirstChanceExceptions(() => obj.TryGetValue(out var value, "url"));

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
