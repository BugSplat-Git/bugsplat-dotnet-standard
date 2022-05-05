using BugSplatDotNetStandard.Http;
using NUnit.Framework;

namespace Tests
{
    public class JsonObjectTest
    {

        [Test]
        public void JsonObject_GetValue_ShouldReturnValueForTopLevelKey()
        {
            var expected = "https://bugsplat.com";
            var json = $@"{{ ""url"": ""{ expected }"" }}";
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
}
