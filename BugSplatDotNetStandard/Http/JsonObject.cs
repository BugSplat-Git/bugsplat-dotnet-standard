using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace BugSplatDotNetStandard.Http
{
    // We have opted to not introduce a 3rd-party dependency to better support Unity.
    // When Unity moves to .NET 6 we can replace this with System.Text.Json.
    // More information about Unity's plans to update to .NET 6 can be found here:
    // https://forum.unity.com/threads/unity-future-net-development-status.1092205/

    public class JsonObject
    {
        private string json;

        public JsonObject(string json)
        {
            this.json = json;
        }
                
        public string GetValue(params string[] path)
        {
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var quotas = new XmlDictionaryReaderQuotas();
            var jsonReader = JsonReaderWriterFactory.CreateJsonReader(jsonBytes, quotas);
            var root = XElement.Load(jsonReader);
            var key = string.Join("/", path);
            return root.XPathSelectElement($"//{key}").Value;
        }
    }
}
