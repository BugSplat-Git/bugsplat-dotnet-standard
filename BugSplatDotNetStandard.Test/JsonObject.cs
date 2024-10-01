using System.Collections.Generic;
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
    }
}
